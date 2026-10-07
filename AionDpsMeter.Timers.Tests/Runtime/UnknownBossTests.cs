using AionDpsMeter.Core.Models;
using AionDpsMeter.Timers.Overlay;

namespace AionDpsMeter.Timers.Tests.Runtime;

public class UnknownBossTests
{
    private static readonly DateTime Started = new(2026, 10, 7, 22, 0, 0);

    /// <summary>
    /// Started mid-fight, the meter never saw the boss spawn: the target has no code, and upstream's boss-only filter
    /// dropped every hit on it. Only a target first seen right after the start is that boss; mobs that were already
    /// standing around and are pulled later stay ordinary (seen in a dungeon: the next pack counted as a boss).
    /// </summary>
    [Fact]
    public void An_unidentified_target_is_a_boss_only_right_after_the_meter_started()
    {
        var policy = UnknownBossPolicy.FirstMinuteAfter(Started);
        Assert.True(policy(new Mob { Id = 1, MobCode = 0, CreatedAt = Started.AddSeconds(5) }));
        Assert.False(policy(new Mob { Id = 2, MobCode = 0, CreatedAt = Started.AddMinutes(3) }));
    }

    [Fact]
    public void Mob_IsBoss_asks_the_policy_only_for_unidentified_targets()
    {
        var before = Mob.UnknownIsBoss;
        try
        {
            Mob.UnknownIsBoss = _ => true;
            Assert.True(new Mob { Id = 1, MobCode = 0 }.IsBoss);
            Assert.False(new Mob { Id = 2, MobCode = 2090664 }.IsBoss); // Mumu Worker: the table decides
            Mob.UnknownIsBoss = _ => false;
            Assert.False(new Mob { Id = 3, MobCode = 0 }.IsBoss);
        }
        finally
        {
            Mob.UnknownIsBoss = before;
        }
    }
}
