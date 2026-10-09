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

    /// <summary>
    /// Upstream reads a player's HP packet (other layout) as 8 bytes of junk: 4.5e18 after PvP, death and revival in
    /// packet logs. Such a target became a "boss" with billions of HP. Real HP fits in 32 bits (bosses 1M–100M).
    /// </summary>
    [Fact]
    public void Junk_hp_beyond_32_bits_is_ignored()
    {
        var target = new Mob { Id = 152, MobCode = 0, HpCurrent = 3_582_050_557_914_920_535 };
        Assert.Equal(0, target.HpCurrent);
        Assert.False(target.IsBoss);

        var boss = new Mob { Id = 24173, MobCode = 0, HpCurrent = 3_598_511 };
        boss.HpCurrent = 4_518_525_502_010_000_000;
        Assert.Equal(3_598_511, boss.HpCurrent);
        Assert.Equal(3_598_511, boss.HpTotal);
    }
}
