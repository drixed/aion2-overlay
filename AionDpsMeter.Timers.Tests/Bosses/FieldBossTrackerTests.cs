using AionDpsMeter.Timers.Bosses;
using AionDpsMeter.Timers.Schedule;
using Microsoft.Extensions.Time.Testing;

namespace AionDpsMeter.Timers.Tests.Bosses;

public class FieldBossTrackerTests
{
    // Block 2400: three field bosses (code order: 2400017, 2400800, 2400900). Block 2500: two, map unknown.
    private static readonly BossCatalog Catalog = BossCatalog.FromJson("""
        { "2400017": { "name": "Данар", "isBoss": true },
          "2400800": { "name": "Гартуа", "isBoss": true },
          "2400900": { "name": "Третий", "isBoss": true },
          "2500001": { "name": "Чужой", "isBoss": true },
          "2500002": { "name": "Чужой 2", "isBoss": true },
          "2000002": { "name": "Моб", "isBoss": false } }
        """);

    private static readonly ScheduleData Data = new([],
        new Dictionary<int, FieldBossMapInfo> { [1110] = new(2400, "Альтгард") },
        new Dictionary<int, int>());

    private readonly FakeTimeProvider time = new(new DateTimeOffset(2026, 10, 7, 12, 0, 0, TimeSpan.Zero));

    private FieldBossTracker Tracker(ScheduleData? data = null) => new(Catalog, () => data ?? Data, time);

    private static FieldBossSlot Slot(int map, int place, bool alive, DateTimeOffset? at) =>
        new(map * 100 + place, alive, at?.ToUnixTimeMilliseconds() ?? 0, 0, 0, 0);

    private static FieldBossList List(int map, int count, params FieldBossSlot[] slots) => new(map, count, slots);

    [Fact]
    public void A_list_of_a_known_map_names_its_slots()
    {
        var t = Tracker();
        var back = time.GetUtcNow().AddHours(2);
        t.OnList(1, List(1110, 3, Slot(1110, 1, true, time.GetUtcNow().AddMinutes(-5)), Slot(1110, 2, false, back)));

        var timers = t.TimersFor(1);
        Assert.True(timers.Single(x => x.Key == 2400017).Alive);
        Assert.Equal(back, timers.Single(x => x.Key == 2400800).NextSpawn);
    }

    [Fact]
    public void Slots_of_an_unknown_map_are_kept_under_their_slot_id()
    {
        var t = Tracker();
        t.OnList(1, List(77, 2, Slot(77, 1, false, time.GetUtcNow().AddHours(1))));
        Assert.Equal(-7701, Assert.Single(t.TimersFor(1)).Key);
    }

    [Fact]
    public void Servers_do_not_share_timers()
    {
        var t = Tracker();
        t.OnList(1, List(1110, 3, Slot(1110, 1, true, time.GetUtcNow())));
        Assert.Single(t.TimersFor(1));
        Assert.Empty(t.TimersFor(2));
    }

    [Fact]
    public void A_kill_followed_by_the_list_teaches_the_respawn_interval()
    {
        var t = Tracker();
        var killedAt = time.GetUtcNow();
        t.OnKill(1, 2400800);
        time.Advance(TimeSpan.FromMinutes(10));
        t.OnList(1, List(1110, 3, Slot(1110, 2, false, killedAt.AddMinutes(120))));
        Assert.Equal(120, t.RespawnMinutesOf(2400800));

        time.Advance(TimeSpan.FromHours(3));
        t.OnKill(1, 2400800);
        Assert.Equal(time.GetUtcNow().AddMinutes(120), t.TimersFor(1).Single(x => x.Key == 2400800).NextSpawn);
    }

    [Fact]
    public void A_kill_without_a_known_interval_has_no_spawn_time_until_data_gives_one()
    {
        var t = Tracker();
        t.OnKill(1, 2400017);
        Assert.Null(t.TimersFor(1).Single().NextSpawn);

        var withData = Tracker(Data with { RespawnMinutes = new Dictionary<int, int> { [2400017] = 60 } });
        withData.OnKill(1, 2400017);
        Assert.Equal(time.GetUtcNow().AddMinutes(60), withData.TimersFor(1).Single().NextSpawn);
    }

    [Fact]
    public void The_same_kill_seen_twice_counts_once()
    {
        var t = Tracker();
        t.OnKill(1, 2400017);
        var first = t.TimersFor(1).Single().LastKill;
        time.Advance(TimeSpan.FromSeconds(30));
        t.OnKill(1, 2400017);
        Assert.Equal(first, t.TimersFor(1).Single().LastKill);
    }

    [Fact]
    public void Kills_of_non_field_bosses_and_non_bosses_are_ignored()
    {
        var t = Tracker();
        t.OnKill(1, 2500001); // boss, but its block belongs to no known or listed map (e.g. a dungeon boss)
        t.OnKill(1, 2000002); // not a boss
        Assert.Empty(t.TimersFor(1));
    }

    [Fact]
    public void A_kill_teaches_the_block_of_a_recently_listed_map()
    {
        var t = Tracker();
        t.OnList(1, List(77, 2, Slot(77, 1, true, time.GetUtcNow()), Slot(77, 2, true, time.GetUtcNow())));
        time.Advance(TimeSpan.FromMinutes(2));
        t.OnKill(1, 2500002);

        var timers = t.TimersFor(1);
        Assert.All(timers, x => Assert.True(x.Key > 0));
        var killed = timers.Single(x => x.Key == 2500002);
        Assert.False(killed.Alive);
        Assert.NotNull(killed.LastKill);
        Assert.True(timers.Single(x => x.Key == 2500001).Alive);
    }

    [Fact]
    public void MarkKilled_works_on_tracked_entries_only()
    {
        var t = Tracker();
        t.OnList(1, List(77, 2, Slot(77, 1, true, time.GetUtcNow())));
        t.MarkKilled(1, -7701);
        t.MarkKilled(1, 999);
        var only = Assert.Single(t.TimersFor(1));
        Assert.False(only.Alive);
        Assert.NotNull(only.LastKill);
    }

    [Fact]
    public void An_identical_list_changes_nothing()
    {
        var t = Tracker();
        var changes = 0;
        t.Changed += () => changes++;
        var list = List(1110, 3, Slot(1110, 1, true, time.GetUtcNow()));
        t.OnList(1, list);
        t.OnList(1, List(1110, 3, Slot(1110, 1, true, time.GetUtcNow())));
        Assert.Equal(1, changes);
    }

    [Fact]
    public void State_survives_a_restart()
    {
        using var dir = new TempDir();
        var store = new TimerStateStore(dir.File("timers-state.json"));
        var t = Tracker();
        t.OnList(1, List(77, 2, Slot(77, 1, true, time.GetUtcNow()), Slot(77, 2, true, time.GetUtcNow())));
        t.OnKill(1, 2500002); // learns block 2500 for map 77
        t.OnKill(1, 2400800);
        time.Advance(TimeSpan.FromMinutes(5));
        t.OnList(1, List(1110, 3, Slot(1110, 2, false, time.GetUtcNow().AddMinutes(115))));
        store.Save(t.Export());

        var restored = Tracker();
        restored.Import(store.Load());

        Assert.Equal(t.TimersFor(1).OrderBy(x => x.Key), restored.TimersFor(1).OrderBy(x => x.Key));
        Assert.Equal(120, restored.RespawnMinutesOf(2400800));
        restored.OnList(1, List(77, 2, Slot(77, 1, false, time.GetUtcNow().AddHours(1))));
        Assert.Contains(restored.TimersFor(1), x => x.Key == 2500001); // learned block came back too
    }

    [Fact]
    public void A_state_file_with_null_entries_imports_the_rest()
    {
        using var dir = new TempDir();
        File.WriteAllText(dir.File("timers-state.json"), """
            { "Timers": [ null, { "ServerId": 1, "Key": 2400017, "MapId": 1110, "SlotId": 111001, "Alive": true } ],
              "LearnedBlocks": null, "LearnedRespawnMinutes": { "2400017": 60 } }
            """);
        var t = Tracker();
        t.Import(new TimerStateStore(dir.File("timers-state.json")).Load());
        Assert.Equal(2400017, Assert.Single(t.TimersFor(1)).Key);
        Assert.Equal(60, t.RespawnMinutesOf(2400017));
    }

    [Fact]
    public void A_missing_or_corrupt_state_file_loads_as_nothing()
    {
        using var dir = new TempDir();
        var store = new TimerStateStore(dir.File("timers-state.json"));
        Assert.Null(store.Load());
        File.WriteAllText(dir.File("timers-state.json"), "{ nope");
        Assert.Null(store.Load());
    }
}
