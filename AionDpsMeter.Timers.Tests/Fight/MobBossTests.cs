using AionDpsMeter.Core.Models;
using AionDpsMeter.Timers.Bosses;

namespace AionDpsMeter.Timers.Tests.Fight;

public class MobBossTests
{
    // 2101244 "Оскверненный гладиатор даэвов" (Corrupted Daeva Gladiator): a boss in the fork's npcs.ru.json, not in
    // upstream's mobs.json — its hits were dropped with "Только боссы".
    [Fact]
    public void A_boss_only_the_fork_table_knows_is_a_boss()
    {
        Mob.ForkBoss = BossCatalog.LoadDefault().IsBoss;
        Assert.True(new Mob { Id = 1, MobCode = 2101244 }.IsBoss);
        Assert.False(new Mob { Id = 2, MobCode = 2090664, HpCurrent = 5_000_000 }.IsBoss); // Mumu Worker stays no boss
        Assert.False(new Mob { Id = 3, MobCode = 2000002 }.IsBoss);                        // an ordinary mob
    }

    [Fact]
    public void A_target_with_dungeon_boss_hp_is_a_boss_whatever_the_tables_say()
    {
        var mob = new Mob { Id = 4, MobCode = 2000002 };
        mob.HpCurrent = 22_809_600;
        Assert.True(mob.IsBoss);
    }
}
