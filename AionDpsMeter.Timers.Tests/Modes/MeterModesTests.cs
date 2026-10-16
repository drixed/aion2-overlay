using AionDpsMeter.Timers.Modes;

namespace AionDpsMeter.Timers.Tests.Modes;

public class DummyRunTests
{
    private static readonly DateTime T0 = new(2026, 10, 13, 12, 0, 0);

    private const long Me = 1;

    private static Hit OnDummy(long player, long damage, double atSeconds) =>
        new(player, damage, OnDummy: true, T0.AddSeconds(atSeconds), Mine: player == Me);

    [Fact]
    public void A_run_starts_with_the_first_hit_on_a_dummy_and_counts_one_minute()
    {
        var run = new DummyRun(TimeSpan.FromMinutes(1));
        run.Add(OnDummy(1, 1_000, 0));
        run.Add(OnDummy(1, 2_000, 30));
        run.Add(OnDummy(2, 3_000, 59.9));
        run.Add(OnDummy(1, 9_999, 61)); // after the minute: not counted

        var view = run.View(T0.AddSeconds(70));
        Assert.True(view.Finished);
        Assert.Equal(TimeSpan.FromMinutes(1), view.Elapsed);
        Assert.Equal(3_000 / 60.0, view.Rows.Single(r => r.PlayerId == 1).PerSecond, 3);
        Assert.Equal(3_000, view.Rows.Single(r => r.PlayerId == 2).Total);
    }

    [Fact]
    public void While_running_it_shows_dps_so_far_and_time_left()
    {
        var run = new DummyRun(TimeSpan.FromMinutes(1));
        run.Add(OnDummy(1, 6_000, 0));
        var view = run.View(T0.AddSeconds(20));
        Assert.False(view.Finished);
        Assert.Equal(TimeSpan.FromSeconds(40), view.Left);
        Assert.Equal(300, view.Rows.Single().PerSecond, 3);
    }

    [Fact]
    public void Hits_on_anything_else_are_ignored_and_a_pause_starts_a_new_run()
    {
        var run = new DummyRun(TimeSpan.FromMinutes(1));
        run.Add(new Hit(1, 5_000, OnDummy: false, T0));
        Assert.Empty(run.View(T0).Rows);

        run.Add(OnDummy(1, 1_000, 0));
        run.Add(OnDummy(1, 4_000, 70)); // 10 s after the first run ended: a new run
        var view = run.View(T0.AddSeconds(71));
        Assert.Equal(4_000, view.Rows.Single().Total);
        Assert.False(view.Finished);
    }

    [Fact]
    public void Hitting_on_after_the_run_keeps_its_result_until_a_pause()
    {
        // 2026-10-16: the user kept hitting the dummy and the result was wiped by a new run a few seconds after the end
        var run = new DummyRun(TimeSpan.FromMinutes(1));
        for (var s = 0; s <= 120; s += 2) run.Add(OnDummy(1, 1_000, s));
        var kept = run.View(T0.AddSeconds(121));
        Assert.True(kept.Finished);
        Assert.Equal(30_000, kept.Rows.Single().Total);             // hits at 0, 2 … 58

        run.Add(OnDummy(1, 7_000, 130));                             // 10 s without hitting: a new run
        Assert.Equal(7_000, run.View(T0.AddSeconds(131)).Rows.Single().Total);
    }

    [Fact]
    public void Each_player_has_an_own_run_and_others_do_not_start_or_reset_mine()
    {
        // 2026-10-16: two strangers on nearby dummies started the run; the user was not in the list
        var run = new DummyRun(TimeSpan.FromMinutes(1));
        run.Add(OnDummy(7, 50_000, 0));
        var before = run.View(T0.AddSeconds(10));
        Assert.False(before.Started);                                // my run has not started: the hint stays
        Assert.Equal(7, before.Rows.Single().PlayerId);

        run.Add(OnDummy(Me, 1_000, 20));
        run.Add(OnDummy(7, 50_000, 66));                             // 6 s after his run ended: his new run
        run.Add(OnDummy(Me, 2_000, 70));
        var view = run.View(T0.AddSeconds(70));
        Assert.True(view.Started);
        Assert.Equal(TimeSpan.FromSeconds(10), view.Left);           // mine started at 20 s
        Assert.Equal(3_000, view.Rows.Single(r => r.PlayerId == Me).Total);
        Assert.Equal(3_000 / 50.0, view.Rows.Single(r => r.PlayerId == Me).PerSecond, 3);
        Assert.Equal(50_000, view.Rows.Single(r => r.PlayerId == 7).Total);
    }
}

public class GrindSessionTests
{
    private static readonly DateTime T0 = new(2026, 10, 13, 12, 0, 0);

    [Fact]
    public void Grind_adds_up_every_target_between_play_and_stop()
    {
        var grind = new GrindSession();
        grind.Add(new Hit(1, 999, false, T0)); // before play: ignored
        grind.Play(T0);
        grind.Add(new Hit(1, 1_000, false, T0.AddSeconds(10)));
        grind.Add(new Hit(1, 2_000, true, T0.AddSeconds(50)));
        grind.Stop(T0.AddSeconds(100));
        grind.Add(new Hit(1, 5_000, false, T0.AddSeconds(110))); // after stop: ignored

        var view = grind.View(T0.AddSeconds(500));
        Assert.False(view.Running);
        Assert.Equal(TimeSpan.FromSeconds(100), view.Elapsed);
        Assert.Equal(3_000, view.Rows.Single().Total);
        Assert.Equal(30, view.Rows.Single().PerSecond, 3);
    }

    [Fact]
    public void Play_again_starts_over()
    {
        var grind = new GrindSession();
        grind.Play(T0);
        grind.Add(new Hit(1, 1_000, false, T0.AddSeconds(1)));
        grind.Play(T0.AddSeconds(5));
        Assert.Empty(grind.View(T0.AddSeconds(6)).Rows);
        Assert.True(grind.View(T0.AddSeconds(6)).Running);
    }
}

public class HealTallyTests
{
    private static readonly DateTime T0 = new(2026, 10, 13, 12, 0, 0);

    [Fact]
    public void Healing_on_others_is_counted_per_caster_within_the_fight()
    {
        var heals = new HealTally();
        heals.Add(new Heal(7, 1, 5_000, T0.AddSeconds(-30))); // before the fight
        heals.Add(new Heal(7, 1, 4_000, T0.AddSeconds(5)));
        heals.Add(new Heal(7, 7, 9_000, T0.AddSeconds(6)));  // on self: not "on others"
        heals.Add(new Heal(8, 2, 2_000, T0.AddSeconds(8)));

        var rows = heals.Rows(T0, T0.AddSeconds(10));
        Assert.Equal([7L, 8L], rows.Select(r => r.PlayerId));
        Assert.Equal(4_000, rows[0].Total);
        Assert.Equal(400, rows[0].PerSecond, 3);
    }
}
