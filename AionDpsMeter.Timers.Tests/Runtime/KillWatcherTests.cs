using AionDpsMeter.Timers.Bosses;
using AionDpsMeter.Timers.Runtime;
using AionDpsMeter.Timers.Schedule;
using Microsoft.Extensions.Time.Testing;

namespace AionDpsMeter.Timers.Tests.Runtime;

internal sealed class FakeServer(int id) : IServerContext
{
    public int CurrentServerId => id;
}

public class KillWatcherTests
{
    private sealed class FakeMobs : IMobHpSource
    {
        public List<MobHp> Mobs { get; } = new();
        public bool Throw { get; set; }
        public IReadOnlyList<MobHp> Snapshot() => Throw ? throw new InvalidOperationException("modified") : Mobs.ToList();
    }

    private static readonly BossCatalog Catalog = BossCatalog.FromJson("""
        { "2400800": { "name": "Гартуа", "isBoss": true }, "2000002": { "name": "Моб", "isBoss": false } }
        """);

    private readonly FakeMobs mobs = new();
    private readonly FieldBossTracker tracker = new(Catalog,
        () => new ScheduleData([], new Dictionary<int, FieldBossMapInfo> { [1110] = new(2400, "Альтгард") }, new Dictionary<int, int>()),
        new FakeTimeProvider(new DateTimeOffset(2026, 10, 7, 12, 0, 0, TimeSpan.Zero)));

    private KillWatcher Watcher() => new(mobs, new FakeServer(7), Catalog, tracker);

    [Fact]
    public void A_boss_seen_alive_then_at_zero_hp_is_a_kill()
    {
        var w = Watcher();
        mobs.Mobs.Add(new MobHp(501, 2400800, 1_000, 400));
        w.Tick();
        mobs.Mobs[0] = mobs.Mobs[0] with { HpCurrent = 0 };
        w.Tick();
        w.Tick(); // still dead: no second kill

        var timer = Assert.Single(tracker.TimersFor(7));
        Assert.Equal(2400800, timer.Key);
        Assert.NotNull(timer.LastKill);
    }

    [Fact]
    public void A_corpse_never_seen_alive_is_not_a_kill()
    {
        var w = Watcher();
        mobs.Mobs.Add(new MobHp(501, 2400800, 1_000, 0));
        w.Tick();
        Assert.Empty(tracker.TimersFor(7));
    }

    [Fact]
    public void Ordinary_mobs_are_ignored()
    {
        var w = Watcher();
        mobs.Mobs.Add(new MobHp(9, 2000002, 100, 50));
        w.Tick();
        mobs.Mobs[0] = mobs.Mobs[0] with { HpCurrent = 0 };
        w.Tick();
        Assert.Empty(tracker.TimersFor(7));
    }

    [Fact]
    public void A_failed_snapshot_is_skipped()
    {
        var w = Watcher();
        mobs.Throw = true;
        w.Tick(); // must not throw
    }
}
