using AionDpsMeter.Core.Data;
using AionDpsMeter.Core.GameData.Services;

namespace AionDpsMeter.Core.Models
{
    public class Mob : Entity
    {
        private const int BossHpThreshold = 100_000_000;

        public int MobCode { get; set; }
        public long HpTotal { get; set; }
        public long HpCurrent
        {
            get;
            set { field = value; HpMaxSeen = Math.Max(HpMaxSeen, value); } // aion2-overlay fork
        }
        /// <summary>aion2-overlay fork: the highest HP seen, so a target whose spawn the meter missed can be sized.</summary>
        public long HpMaxSeen { get; private set; }
        /// <summary>aion2-overlay fork: bosses in dungeons have 3.6M+ HP, ordinary mobs at most ~300K.</summary>
        public const long UnknownBossHp = 1_000_000;
        //public new string Name => GetMobName();
        //public bool IsBoss => CanBeBoss();

        public new string Name => GameDataProvider.Instance.GetMobName(MobCode);
        public bool IsBoss => MobCode == 0 ? HpMaxSeen >= UnknownBossHp : GameDataProvider.Instance.IsBoss(MobCode); // aion2-overlay fork: spawn unseen (meter started mid-dungeon) — judge by HP

        public bool IsDummy => GameDataProvider.Instance.IsDummy(MobCode);

        private string GetMobName()
        {
            if (GameDataProvider.Instance.IsKnownMob(MobCode)) return GameDataProvider.Instance.GetMobName(MobCode);
            if (HpTotal >= BossHpThreshold) return $"Unknown boss {MobCode}";
            return $"Unknown {MobCode}";
        }

        private bool CanBeBoss()
        {
            if (GameDataProvider.Instance.IsKnownMob(MobCode)) return GameDataProvider.Instance.IsBoss(MobCode);
            if (HpTotal >= BossHpThreshold) return true;
            return false;
        }
    }
}
