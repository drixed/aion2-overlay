using AionDpsMeter.Core.Models;

namespace AionDpsMeter.Timers.Tests.Runtime;

public class MobHpTests
{
    /// <summary>
    /// Upstream's mob-info parser sometimes reads junk as the max HP (67, 71, 4655… in packet logs, Oct 2026); a boss
    /// that left its zone and came back got 67 and its bar sat at 100%. Max HP is never below an HP the boss had.
    /// </summary>
    [Fact]
    public void A_junk_max_hp_below_the_seen_hp_is_ignored()
    {
        var boss = new Mob { Id = 1, MobCode = 2939202, HpCurrent = 5_000_000 };
        boss.HpCurrent = 4_290_000;
        boss.HpTotal = 67;
        Assert.Equal(5_000_000, boss.HpTotal);
    }

    [Fact]
    public void A_real_max_hp_stays()
    {
        var boss = new Mob { Id = 1, MobCode = 2939202, HpTotal = 6_800_000, HpCurrent = 4_290_000 };
        Assert.Equal(6_800_000, boss.HpTotal);
    }
}
