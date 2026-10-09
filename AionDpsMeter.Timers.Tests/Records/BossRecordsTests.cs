using AionDpsMeter.Services.Models;
using AionDpsMeter.Services.Services.Session;
using AionDpsMeter.Services.Services.Session.Persistence;
using AionDpsMeter.Timers.Records;
using Microsoft.Extensions.Logging.Abstractions;

namespace AionDpsMeter.Timers.Tests.Records;

public sealed class BossRecordsTests : IDisposable
{
    private static readonly DateTime Start = new(2026, 10, 9, 20, 0, 0);
    private readonly string path = Path.Combine(Path.GetTempPath(), $"boss-records-{Guid.NewGuid():N}.json");

    public void Dispose() => File.Delete(path);

    private BossRecords NewRecords() => new(path, TimeProvider.System, NullLogger<BossRecords>.Instance);

    private static HistorySessionSnapshot Fight(double seconds, long myDamage = 2_000_000, Guid? id = null) => new()
    {
        SessionId = id ?? Guid.NewGuid(),
        TargetName = "Red Spark Ignus",
        TargetHpTotal = 8_208_000,
        SessionStart = Start,
        SessionEnd = Start.AddSeconds(seconds),
        PlayerStats =
        [
            new PlayerStats { PlayerId = 1, IsUser = true, TotalDamage = myDamage },
            new PlayerStats { PlayerId = 2, TotalDamage = 8_216_291 - myDamage },
        ],
    };

    [Fact]
    public void A_kill_is_kept_across_restarts()
    {
        NewRecords().Observe(Fight(80));
        var r = NewRecords().For("Red Spark Ignus", 8_208_000)!;
        Assert.Equal(TimeSpan.FromSeconds(80), r.BestKill);
        Assert.Equal(2_000_000 / 80.0, r.BestMyDps, 3);
    }

    [Fact]
    public void The_same_fight_saved_twice_counts_once()
    {
        var records = NewRecords();
        var id = Guid.NewGuid();
        records.Observe(Fight(80, id: id));
        records.Observe(Fight(80, id: id));
        Assert.Equal(1, records.For("Red Spark Ignus", 8_208_000)!.Kills);
    }

    [Fact]
    public void A_faster_kill_is_announced()
    {
        var records = NewRecords();
        records.Observe(Fight(80));
        Assert.Null(records.Latest); // the first kill is not news worth a banner
        records.Observe(Fight(68));
        Assert.Equal(RecordNews.FasterKill, records.Latest!.News);
        Assert.Equal(TimeSpan.FromSeconds(80), records.Latest.Record.PreviousBestKill);
    }

    [Fact]
    public void The_first_run_fills_the_book_from_history_once()
    {
        var history = new FakeHistory([Fight(80), Fight(70), Fight(20, myDamage: 1)]);
        history.Sessions[2] = new HistorySessionSnapshot { SessionId = history.Sessions[2].SessionId, TargetName = "Red Spark Ignus", TargetHpTotal = 8_208_000, SessionStart = Start, SessionEnd = Start.AddSeconds(20), PlayerStats = [new PlayerStats { IsUser = true, TotalDamage = 100 }] }; // a wipe
        NewRecords().BackfillIfNew(history);
        var r = NewRecords().For("Red Spark Ignus", 8_208_000)!;
        Assert.Equal(2, r.Kills);
        Assert.Equal(TimeSpan.FromSeconds(70), r.BestKill);

        var again = NewRecords();
        again.BackfillIfNew(history); // the file exists: nothing is added twice
        Assert.Equal(2, again.For("Red Spark Ignus", 8_208_000)!.Kills);
    }

    private sealed class FakeHistory(List<HistorySessionSnapshot> sessions) : ICombatHistoryStore
    {
        public List<HistorySessionSnapshot> Sessions { get; } = sessions;
        public void Save(HistorySessionSnapshot snapshot) { }
        public void FlushPendingSaves() { }
        public IReadOnlyList<HistorySessionListItem> GetSessionList() => Sessions.Select(s => new HistorySessionListItem
        {
            SessionId = s.SessionId, TargetName = s.TargetName, TargetHpTotal = s.TargetHpTotal,
            SessionStart = s.SessionStart, SessionEnd = s.SessionEnd, TotalDamage = s.PlayerStats.Sum(p => p.TotalDamage),
        }).ToList();
        public int GetSessionCount(DateTime? dateFrom, DateTime? dateTo, string? bossNameContains, IReadOnlySet<Guid>? excludeSessionIds = null) => Sessions.Count;
        public IReadOnlyList<HistorySessionListItem> GetSessionPage(DateTime? dateFrom, DateTime? dateTo, string? bossNameContains, int skip, int take, IReadOnlySet<Guid>? excludeSessionIds = null) => GetSessionList();
        public HistorySessionSnapshot? GetSession(Guid sessionId) => Sessions.FirstOrDefault(s => s.SessionId == sessionId);
    }
}
