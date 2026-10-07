// aion2-overlay fork: new file (see FORK_CHANGES.md).
using AionDpsMeter.Timers.Bosses;
using AionDpsMeter.Timers.Feed;
using AionDpsMeter.Timers.Runtime;
using AionDpsMeter.Timers.Schedule;
using AionDpsMeter.UI.Services.TimerWindow;

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
        TimeProvider time)
    {
        private static readonly TimeSpan ToastFor = TimeSpan.FromSeconds(15);

        private CancellationTokenSource? cts;
        private IReadOnlyList<FeedItem> visible = [];
        private DateTimeOffset now;
        private string? toast;
        private DateTimeOffset toastUntil;

        // The markup (the other half of this partial class) cannot see primary-constructor parameters.
        private TimersOptions Options => optionsStore.Current;
        private bool IsEditing => controller.IsEditing;
        private void Drag() => controller.Drag();

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
                    Recompute();
                    await InvokeAsync(StateHasChanged);
                }
                while (await timer.WaitForNextTickAsync(ct));
            }
            catch (OperationCanceledException) { }
        }

        private void Recompute()
        {
            now = time.GetUtcNow();
            var items = TimersFeed.Build(now, schedule.Current, tracker.TimersFor(server.CurrentServerId), catalog)
                .Where(Options.Shows)
                .ToList();
            var due = alerts.Due(items, now, Options.LeadFor);
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

        private void ShowAll()
        {
            Options.HiddenIds.Clear();
            optionsStore.Save();
            Recompute();
        }

        private void ToggleKind(FeedKind kind, bool show)
        {
            if (show) Options.HiddenKinds.Remove(kind);
            else Options.HiddenKinds.Add(kind);
            optionsStore.Save();
            Recompute();
        }

        private static string KindIcon(FeedKind kind) => kind switch
        {
            FeedKind.Rift => "◈",
            FeedKind.Boss => "☠",
            _ => "★",
        };

        private static string KindLabel(FeedKind kind) => kind switch
        {
            FeedKind.Rift => "Разломы",
            FeedKind.Boss => "Боссы",
            _ => "Ивенты",
        };

        public void Dispose()
        {
            controller.EditingChanged -= OnEditingChanged;
            cts?.Cancel();
            cts?.Dispose();
        }
    }
}
