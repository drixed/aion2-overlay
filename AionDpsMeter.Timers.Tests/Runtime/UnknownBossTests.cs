using AionDpsMeter.Core.Models;

namespace AionDpsMeter.Timers.Tests.Runtime;

public class UnknownBossTests
{
    /// <summary>
    /// Started mid-dungeon, the meter never saw the spawn of what was already standing there: those targets have no
    /// code. Their HP still arrives — bosses there have 3.6–6.8M, ordinary mobs at most ~300K (packet logs, Oct 2026).
    /// </summary>
    [Fact]
    public void An_unidentified_target_with_boss_sized_hp_is_a_boss()
    {
        var boss = new Mob { Id = 1, MobCode = 0, HpCurrent = 3_598_511 };
        boss.HpCurrent = 400_000; // the fight goes on: it stays a boss
        Assert.True(boss.IsBoss);
    }

    [Fact]
    public void An_unidentified_ordinary_mob_is_not_a_boss()
    {
        Assert.False(new Mob { Id = 2, MobCode = 0, HpCurrent = 146_900 }.IsBoss);
        Assert.False(new Mob { Id = 3, MobCode = 0 }.IsBoss); // no HP seen yet
    }

    [Fact]
    public void An_identified_mob_is_judged_by_the_game_data_not_by_hp()
    {
        Assert.False(new Mob { Id = 4, MobCode = 2090664, HpCurrent = 5_000_000 }.IsBoss); // Mumu Worker
    }
}
