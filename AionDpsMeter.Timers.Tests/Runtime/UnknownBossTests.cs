using AionDpsMeter.Core.Models;

namespace AionDpsMeter.Timers.Tests.Runtime;

public class UnknownBossTests
{
    /// <summary>
    /// Started mid-fight, the meter never saw the boss spawn: the target has code 0, and its total HP is often unknown
    /// too (upstream records it only above 10M). With "boss encounters only" on, upstream dropped every hit on it.
    /// An unidentified target now counts as a boss; identified mobs keep their table flag.
    /// </summary>
    [Fact]
    public void An_unidentified_target_counts_as_a_boss_and_known_mobs_keep_their_flag()
    {
        Assert.True(new Mob { Id = 1, MobCode = 0, HpTotal = 126_320_000 }.IsBoss);
        Assert.True(new Mob { Id = 2, MobCode = 0, HpCurrent = 600_000 }.IsBoss); // dungeon boss, total never recorded
        Assert.False(new Mob { Id = 3, MobCode = 2090664 }.IsBoss);               // Mumu Worker, an ordinary mob
    }
}
