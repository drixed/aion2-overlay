// aion2-overlay fork: new file (see FORK_CHANGES.md).
using AionDpsMeter.Core.GameData.Services;

namespace AionDpsMeter.Services.PacketProcessing.Fork
{
    /// <summary>
    /// A short diagnostic log for the healing check (Nov 2026: a healer's whole fight showed ~1K healing): every player
    /// skill that lands on a player or on its caster, with the packet's type byte and whether the healing list knows it.
    /// One fight with a healer shows which skill codes still heal unseen. Logs/heal-probe.txt, at most 4000 lines a day.
    /// </summary>
    public static class HealProbe
    {
        private const int MaxLinesPerDay = 4000;
        private static readonly object Gate = new();
        private static DateOnly day;
        private static int lines;

        /// <summary>Settings → «Диагностика» → «Журнал лечения»; off by default.</summary>
        public static volatile bool Enabled;

        public static void Record(string source, int actorId, int targetId, int skillCode, long type, long amount, bool targetIsPlayer)
        {
            if (!Enabled || (!targetIsPlayer && actorId != targetId)) return;
            try
            {
                var data = GameDataProvider.Instance;
                if (data.GetClassBySkillCode(skillCode) is null && !data.IsTheostone(skillCode)) return;
                var line = string.Join('\t', DateTime.Now.ToString("HH:mm:ss.fff"), source, actorId, targetId, skillCode,
                    data.GetSkillOrDefault(skillCode).Name, type, amount, data.IsHealingSkill(skillCode) ? "heal-list" : "-", actorId == targetId ? "self" : "-");
                lock (Gate)
                {
                    var today = DateOnly.FromDateTime(DateTime.Now);
                    if (today != day) { day = today; lines = 0; }
                    if (lines >= MaxLinesPerDay) return;
                    lines++;
                    var dir = Path.Combine(AppContext.BaseDirectory, "Logs");
                    Directory.CreateDirectory(dir);
                    File.AppendAllText(Path.Combine(dir, "heal-probe.txt"), line + Environment.NewLine);
                }
            }
            catch
            {
                // a diagnostic never breaks parsing
            }
        }
    }
}
