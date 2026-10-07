using AionDpsMeter.Core.Data;
using AionDpsMeter.Core.GameData.Services;

namespace AionDpsMeter.Core.Models
{
    public class Mob : Entity
    {
        private const int BossHpThreshold = 100_000_000;

        public int MobCode { get; set; }
        public long HpTotal { get; set; }
        public long HpCurrent { get; set; }
        //public new string Name => GetMobName();
        //public bool IsBoss => CanBeBoss();

        public new string Name => GameDataProvider.Instance.GetMobName(MobCode);
        public bool IsBoss => MobCode == 0 ? UnknownIsBoss(this) : GameDataProvider.Instance.IsBoss(MobCode); // aion2-overlay fork
        /// <summary>aion2-overlay fork: whether a target whose spawn the meter never saw (code 0) is a boss.</summary>
        public static Func<Mob, bool> UnknownIsBoss { get; set; } = _ => false;

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
