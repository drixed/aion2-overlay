// aion2-overlay fork: new file (see FORK_CHANGES.md).
using AionDpsMeter.Core.Models;

namespace AionDpsMeter.Services.PacketProcessing.Fork
{
    public readonly record struct HealEvent(int CasterId, int TargetId, long Amount, int SkillCode, DateTime At);

    /// <summary>Any hit as parsed, also a mob's hit on a player (upstream drops those as "unknown class").</summary>
    public readonly record struct RawHit(int ActorId, int TargetId, long Damage, int SkillCode, bool Critical, DateTime At);

    /// <summary>
    /// Every hit before upstream's filters (boss-only mode drops mob hits) and every heal, which upstream parses and
    /// throws away: the meter's Damage/Heal/Dummy/Grind modes listen here. A listener that throws never breaks parsing.
    /// </summary>
    public static class CombatFeed
    {
        public static event Action<PlayerDamage>? Damage;
        public static event Action<HealEvent>? Heal;
        public static event Action<RawHit>? Hit;
        /// <summary>A player of the meter died (entity id, time).</summary>
        public static event Action<int, DateTime>? Death;
        /// <summary>A monster died (entity id, monster code or 0, time) — for pets: which monster gave the soul.</summary>
        public static event Action<int, int, DateTime>? MobDeath;

        public static void PublishDamage(PlayerDamage hit)
        {
            try { Damage?.Invoke(hit); } catch { }
        }

        public static void PublishHeal(HealEvent heal)
        {
            try { Heal?.Invoke(heal); } catch { }
        }

        public static void PublishHit(RawHit hit)
        {
            try { Hit?.Invoke(hit); } catch { }
        }

        public static void PublishDeath(int entityId, DateTime at)
        {
            try { Death?.Invoke(entityId, at); } catch { }
        }

        public static void PublishMobDeath(int entityId, int mobCode, DateTime at)
        {
            try { MobDeath?.Invoke(entityId, mobCode, at); } catch { }
        }
    }
}
