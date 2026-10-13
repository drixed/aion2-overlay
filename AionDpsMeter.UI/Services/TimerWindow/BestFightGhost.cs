// aion2-overlay fork: new file (see FORK_CHANGES.md).
using AionDpsMeter.Services.Models;
using AionDpsMeter.Services.Services.Session;
using AionDpsMeter.Timers.Records;
using Microsoft.Extensions.DependencyInjection;

namespace AionDpsMeter.UI.Services.TimerWindow
{
    /// <summary>
    /// My best fight with this boss as a graph line behind the live one (LOXIN draws it the same way): the fight of my
    /// best DPS record, loaded once from the history and cached.
    /// </summary>
    public static class BestFightGhost
    {
        private static readonly object Gate = new();
        private static Guid? cachedId;
        private static IReadOnlyList<DpsDataPoint>? cached;

        public static IReadOnlyList<DpsDataPoint>? For(string boss, long hpTotal, bool isUser)
        {
            if (!isUser || string.IsNullOrEmpty(boss) || hpTotal <= 0) return null;
            try
            {
                var services = App.AppHost.Services;
                if (services.GetService<BossRecords>()?.For(boss, hpTotal)?.BestMyDpsSession is not { } id) return null;
                lock (Gate)
                {
                    if (cachedId == id) return cached;
                    cachedId = id;
                    cached = null;
                    var fight = services.GetService<CombatSessionManager>()?.GetHistorySession(id);
                    var me = fight?.PlayerStats.FirstOrDefault(p => p.IsUser);
                    if (fight is not null && me is not null && fight.HitsByPlayer.TryGetValue(me.PlayerId, out var hits))
                        cached = GraphDataCalculator.Compute(hits, []).Points;
                    return cached;
                }
            }
            catch (Exception)
            {
                return null; // the graph without the line is still the graph
            }
        }
    }
}
