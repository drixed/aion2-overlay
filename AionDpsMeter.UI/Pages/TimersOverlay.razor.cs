// aion2-overlay fork: new file (see FORK_CHANGES.md).
using AionDpsMeter.Timers.Bosses;
using AionDpsMeter.Timers.Feed;
using AionDpsMeter.Timers.Runtime;
using AionDpsMeter.Timers.Schedule;
using AionDpsMeter.UI.Services.TimerWindow;
using Microsoft.Extensions.Logging;

namespace AionDpsMeter.UI.Pages
{
    partial class TimersOverlay(
        TimersWindowController controller,
        FieldBossTracker tracker,
        ScheduleSource schedule,
        BossCatalog catalog,
        IServerContext server,
        AlertService alerts,
        TimersOptionsStore optionsStore,
        TimeProvider time,
        ILogger<TimersOverlay> logger)
    {
        private static readonly TimeSpan ToastFor = TimeSpan.FromSeconds(15);

        private CancellationTokenSource? cts;
        private IReadOnlyList<FeedItem> visible = [];
        private DateTimeOffset now;
        private string? toast;
        private DateTimeOffset toastUntil;

        // The markup (the other half of this partial class) cannot see primary-constructor parameters.
        private TimersOptions Options => optionsStore.Current;
        private bool Collapsed => Options.Collapsed;
        private void Drag() => controller.Drag();
        private void HideWindow()
        {
            Options.ShowBossTimers = false;
            optionsStore.Save(); // the controller closes the window
        }
        private void ToggleCollapsed() => controller.SetCollapsed(!Options.Collapsed);

        protected override void OnInitialized()
        {
            controller.EditingChanged += OnEditingChanged;
            cts = new CancellationTokenSource();
            _ = RunAsync(cts.Token);
        }

        private async Task RunAsync(CancellationToken ct)
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
            try
            {
                do
                {
                    try
                    {
                        Recompute();
                        await InvokeAsync(StateHasChanged);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        logger.LogError(ex, "Timers overlay refresh failed; continuing"); // keep the overlay alive
                    }
                }
                while (await timer.WaitForNextTickAsync(ct));
            }
            catch (OperationCanceledException) { }
        }

        private void Recompute()
        {
            now = time.GetUtcNow();
            var bosses = tracker.TimersFor(server.CurrentServerId);
            // Alerts cover rifts and events too; the rows here are field bosses only (the rift sits in the main window).
            var all = TimersFeed.Build(now, schedule.Current, bosses, catalog).Where(Options.Shows).ToList();
            var items = TimersFeed.BuildBosses(now, schedule.Current, bosses, catalog).Where(Options.Shows).ToList();
            var due = alerts.Due(all, now, Options.LeadFor);
            if (due.Count > 0)
            {
                toast = string.Join(" · ", due.Select(d => $"{d.Title} {FeedText.Format(d, now, TimeZoneInfo.Local)}"));
                toastUntil = now + ToastFor;
                if (Options.Sound) System.Media.SystemSounds.Exclamation.Play();
            }
            else if (now > toastUntil)
            {
                toast = null;
            }
            visible = items;
        }

        private void OnEditingChanged() => InvokeAsync(StateHasChanged);

        private void MarkKilled(FeedItem item)
        {
            if (item.BossKey is { } key) tracker.MarkKilled(server.CurrentServerId, key);
            Recompute();
        }

        private void Hide(FeedItem item)
        {
            Options.HiddenIds.Add(item.Id);
            optionsStore.Save();
            Recompute();
        }

        private static string KindIcon(FeedKind kind) => kind switch
        {
            FeedKind.Rift => "◈",
            FeedKind.Boss => "☠",
            _ => "★",
        };

        public void Dispose()
        {
            controller.EditingChanged -= OnEditingChanged;
            cts?.Cancel();
            cts?.Dispose();
        }
    }
}
