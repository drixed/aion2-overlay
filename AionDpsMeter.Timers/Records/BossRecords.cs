using System.Text.Json;
using AionDpsMeter.Services.Services.Session;
using AionDpsMeter.Services.Services.Session.Persistence;
using Microsoft.Extensions.Logging;

namespace AionDpsMeter.Timers.Records;

/// <summary>What the last finished fight beat, for the banner in the main window.</summary>
public sealed record RecordAnnouncement(RecordNews News, BossRecord Record, double MyDps, DateTimeOffset At);

/// <summary>
/// The user's boss records, kept in boss-records.json for good (upstream's fight history is deleted after 30 days).
/// Every saved fight goes through <see cref="Observe"/>; on the first run the existing history fills the book.
/// </summary>
public sealed class BossRecords(string path, TimeProvider time, ILogger<BossRecords> logger)
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    private readonly Lock gate = new();
    private readonly BossRecordBook book = new();
    private readonly HashSet<Guid> seen = [];
    private bool loaded;
    private bool needsBackfill;

    public event Action? Changed;

    public RecordAnnouncement? Latest { get; private set; }

    public BossRecord? For(string boss, long hpTotal)
    {
        lock (gate) { EnsureLoaded(); return book.For(boss, hpTotal); }
    }

    public IReadOnlyList<BossRecord> All()
    {
        lock (gate) { EnsureLoaded(); return book.All.OrderByDescending(r => r.LastKillAt).ToList(); }
    }

    /// <summary>A fight was saved to the history.</summary>
    public void Observe(HistorySessionSnapshot fight)
    {
        RecordNews news;
        BossRecord? record;
        var result = ResultOf(fight, fight.PlayerStats.FirstOrDefault(p => p.IsUser)?.TotalDamage ?? 0);
        lock (gate)
        {
            EnsureLoaded();
            if (!seen.Add(fight.SessionId)) return;
            news = book.Add(result);
            if (news == RecordNews.None && !BossRecordBook.IsKill(result)) return;
            record = book.For(result.Boss, result.HpTotal);
            if (news is RecordNews.FasterKill or RecordNews.BetterMyDps && record is not null)
                Latest = new RecordAnnouncement(news, record, result.MyDamage / result.Duration.TotalSeconds, time.GetUtcNow());
            Save();
        }
        Changed?.Invoke();
    }

    /// <summary>First run only (no file yet): every kill still in upstream's history, oldest first.</summary>
    public void BackfillIfNew(ICombatHistoryStore history)
    {
        lock (gate)
        {
            EnsureLoaded();
            if (!needsBackfill) return;
            needsBackfill = false;
        }
        try
        {
            var kills = history.GetSessionList()
                .Where(s => BossRecordBook.IsKill(new FightResult(s.TargetName, s.TargetHpTotal, s.TotalDamage, s.Duration, 0, s.SessionEnd)))
                .OrderBy(s => s.SessionEnd)
                .ToList();
            lock (gate)
            {
                foreach (var item in kills)
                {
                    if (!seen.Add(item.SessionId)) continue;
                    var my = history.GetSession(item.SessionId)?.PlayerStats.FirstOrDefault(p => p.IsUser)?.TotalDamage ?? 0;
                    book.Add(new FightResult(item.TargetName, item.TargetHpTotal, item.TotalDamage, item.Duration, my, item.SessionEnd));
                }
                Save(); // also when empty: the file marks the backfill as done
            }
            logger.LogInformation("Boss records filled from history: {Kills} kills", kills.Count);
            Changed?.Invoke();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Boss records could not be filled from history");
        }
    }

    public void Clear()
    {
        lock (gate)
        {
            EnsureLoaded();
            book.Clear();
            Latest = null;
            Save();
        }
        Changed?.Invoke();
    }

    private static FightResult ResultOf(HistorySessionSnapshot fight, long myDamage) =>
        new(fight.TargetName, fight.TargetHpTotal, fight.PlayerStats.Sum(p => p.TotalDamage), fight.Duration, myDamage, fight.SessionEnd);

    private void EnsureLoaded()
    {
        if (loaded) return;
        loaded = true;
        needsBackfill = !File.Exists(path);
        try
        {
            if (File.Exists(path))
                book.Import(JsonSerializer.Deserialize<List<BossRecord>>(File.ReadAllText(path), Json) ?? []);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Boss records could not be read; starting empty");
        }
    }

    private void Save()
    {
        try
        {
            var tmp = path + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(book.All.ToList(), Json));
            File.Move(tmp, path, overwrite: true);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Boss records could not be saved");
        }
    }
}

/// <summary>Upstream's history store with one addition: every saved fight is offered to the boss records.</summary>
public sealed class RecordingHistoryStore : ICombatHistoryStore
{
    private readonly CombatHistoryStore inner;
    private readonly BossRecords records;
    private readonly ILogger<RecordingHistoryStore> logger;

    /// <summary>Upstream resolves the store at startup, so the first-run fill starts with the app.</summary>
    public RecordingHistoryStore(CombatHistoryStore inner, BossRecords records, ILogger<RecordingHistoryStore> logger)
    {
        this.inner = inner;
        this.records = records;
        this.logger = logger;
        _ = Task.Run(() => records.BackfillIfNew(inner));
    }

    public void Save(HistorySessionSnapshot snapshot)
    {
        inner.Save(snapshot);
        try { records.Observe(snapshot); }
        catch (Exception ex) { logger.LogWarning(ex, "Boss records skipped a fight"); }
    }

    public void FlushPendingSaves() => inner.FlushPendingSaves();
    public IReadOnlyList<HistorySessionListItem> GetSessionList() => inner.GetSessionList();
    public int GetSessionCount(DateTime? dateFrom, DateTime? dateTo, string? bossNameContains, IReadOnlySet<Guid>? excludeSessionIds = null) =>
        inner.GetSessionCount(dateFrom, dateTo, bossNameContains, excludeSessionIds);
    public IReadOnlyList<HistorySessionListItem> GetSessionPage(DateTime? dateFrom, DateTime? dateTo, string? bossNameContains, int skip, int take, IReadOnlySet<Guid>? excludeSessionIds = null) =>
        inner.GetSessionPage(dateFrom, dateTo, bossNameContains, skip, take, excludeSessionIds);
    public HistorySessionSnapshot? GetSession(Guid sessionId) => inner.GetSession(sessionId);
}
