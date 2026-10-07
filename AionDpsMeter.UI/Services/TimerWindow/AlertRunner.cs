// aion2-overlay fork: new file (see FORK_CHANGES.md).
using System.Windows.Threading;
using AionDpsMeter.Timers.Bosses;
using AionDpsMeter.Timers.Feed;
using AionDpsMeter.Timers.Runtime;
using AionDpsMeter.Timers.Schedule;
using Microsoft.Extensions.Logging;

namespace AionDpsMeter.UI.Services.TimerWindow
{
    /// <summary>
    /// Alerts for rifts, events and field bosses, once a second and independent of which windows are open: the sound
    /// plays here, and the text is shown by the main window (and the timers window, when it is open).
    /// </summary>
    public sealed class AlertRunner(
        TimersOptionsStore options,
        ScheduleSource schedule,
        FieldBossTracker tracker,
        BossCatalog catalog,
        IServerContext server,
        AlertService alerts,
        TimeProvider time,
        ILogger<AlertRunner> logger)
    {
        private static readonly TimeSpan ShowFor = TimeSpan.FromSeconds(15);

        private DispatcherTimer? timer;
        private string? text;
        private DateTimeOffset until;

        /// <summary>The alert to show now, or null.</summary>
        public string? ActiveText => text is not null && time.GetUtcNow() < until ? text : null;

        public void Start()
        {
            if (timer is not null) return;
            timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            timer.Tick += (_, _) => Check();
            timer.Start();
        }

        private void Check()
        {
            try
            {
                var now = time.GetUtcNow();
                var o = options.Current;
                var items = TimersFeed.Build(now, schedule.Current, tracker.TimersFor(server.CurrentServerId), catalog)
                    .Where(o.Notifies);
                var due = alerts.Due(items, now, o.LeadFor);
                if (due.Count == 0) return;
                text = string.Join(" · ", due.Select(d => $"{d.Title} {FeedText.Format(d, now, TimeZoneInfo.Local)}"));
                until = now + ShowFor;
                if (o.Sound) System.Media.SystemSounds.Exclamation.Play();
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Alert check failed");
            }
        }
    }
}
