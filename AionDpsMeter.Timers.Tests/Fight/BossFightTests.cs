using AionDpsMeter.Timers.Fight;

namespace AionDpsMeter.Timers.Tests.Fight;

public class BossFightTests
{
    private static readonly IReadOnlyDictionary<int, int> NoOverrides = new Dictionary<int, int>();

    [Fact]
    public void Time_to_kill_is_remaining_hp_over_party_dps()
    {
        var f = BossFight.Build("Pinopi", 650_000, 1_200_000, 10_000, TimeSpan.FromSeconds(57), null);
        Assert.Equal(TimeSpan.FromSeconds(65), f.ToKill);
        Assert.InRange(f.HpPercent, 54.16, 54.17);
    }

    [Fact]
    public void No_dps_no_hp_or_an_absurd_estimate_shows_nothing()
    {
        Assert.Null(BossFight.Build("B", 650_000, 1_200_000, 0, TimeSpan.Zero, null).ToKill);
        Assert.Null(BossFight.Build("B", 0, 1_200_000, 10_000, TimeSpan.Zero, null).ToKill);
        Assert.Null(BossFight.Build("B", 1_000_000_000, 1_000_000_000, 1, TimeSpan.Zero, null).ToKill);
        Assert.Equal(0, BossFight.Build("B", 5, 0, 1, TimeSpan.Zero, null).HpPercent);
    }

    [Fact]
    public void The_first_moments_of_a_fight_give_no_estimate()
    {
        // Upstream's party DPS is damage / duration: right after the first hit the duration is 0 and the DPS infinite.
        Assert.Null(BossFight.Build("B", 650_000, 1_200_000, double.PositiveInfinity, TimeSpan.Zero, null).ToKill);
        Assert.Null(BossFight.Build("B", 650_000, 1_200_000, double.NaN, TimeSpan.Zero, null).ToKill);
        Assert.Null(BossFight.Build("B", 650_000, 1_200_000, 500_000, TimeSpan.FromSeconds(1), null).ToKill);
        Assert.NotNull(BossFight.Build("B", 650_000, 1_200_000, 10_000, BossFight.MinimumSample, null).ToKill);
    }

    [Fact]
    public void Enrage_counts_down_from_the_fight_start()
    {
        var f = BossFight.Build("B", 1, 2, 1, TimeSpan.FromSeconds(57), BossFight.DefaultEnrage);
        Assert.Equal(new TimeSpan(0, 4, 3), f.ToEnrage); // the screenshot: 0:57 into the fight, 4:03 to enrage
        Assert.False(f.Enraged);

        var late = BossFight.Build("B", 1, 2, 1, TimeSpan.FromSeconds(310), BossFight.DefaultEnrage);
        Assert.Equal(TimeSpan.Zero, late.ToEnrage);
        Assert.True(late.Enraged);
    }

    [Fact]
    public void Enrage_timer_comes_from_overrides_then_boss_kind()
    {
        var overrides = new Dictionary<int, int> { [2900001] = 420, [2900002] = 0 };
        Assert.Equal(TimeSpan.FromMinutes(7), BossFight.EnrageFor(2900001, isFieldBoss: false, overrides));
        Assert.Null(BossFight.EnrageFor(2900002, isFieldBoss: false, overrides));
        Assert.Null(BossFight.EnrageFor(2400800, isFieldBoss: true, NoOverrides));
        Assert.Equal(TimeSpan.FromMinutes(5), BossFight.EnrageFor(2900003, isFieldBoss: false, NoOverrides));
    }
}
