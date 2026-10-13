// aion2-overlay fork: new file (see FORK_CHANGES.md).
using AionDpsMeter.Core.Models;

namespace AionDpsMeter.Services.PacketProcessing.Fork
{
    public readonly record struct HealEvent(int CasterId, int TargetId, long Amount, int SkillCode, DateTime At);

    /// <summary>
    /// Every hit before upstream's filters (boss-only mode drops mob hits) and every heal, which upstream parses and
    /// throws away: the meter's Damage/Heal/Dummy/Grind modes listen here. A listener that throws never breaks parsing.
    /// </summary>
    public static class CombatFeed
    {
        public static event Action<PlayerDamage>? Damage;
        public static event Action<HealEvent>? Heal;

        public static void PublishDamage(PlayerDamage hit)
        {
            try { Damage?.Invoke(hit); } catch { }
        }

        public static void PublishHeal(HealEvent heal)
        {
            try { Heal?.Invoke(heal); } catch { }
        }
    }
}
