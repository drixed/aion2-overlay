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
        AlertRunner alertRunner,
        TimersOptionsStore optionsStore,
        TimeProvider time,
        ILogger<TimersOverlay> logger)
    {

        private CancellationTokenSource? cts;
        private IReadOnlyList<FeedItem> visible = [];
        private DateTimeOffset now;
        private string? toast;

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
            visible = TimersFeed.BuildBosses(now, schedule.Current, bosses, catalog).Where(Options.Shows).ToList();
            toast = alertRunner.ActiveText; // alerts (with sound) come from AlertRunner, whether this window is open or not
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

        private static readonly (string Id, string Label)[] Tabs = [("all", "Все"), ("alive", "Живые"), ("soon", "Скоро")];
        private string tab = "all";
        private int naming;

        private IReadOnlyList<(int Code, string Name)> BossNames => catalog.Bosses();

        /// <summary>Spawns within half an hour, or already late.</summary>
        private bool Soon(FeedItem i) =>
            i.Status == FeedStatus.Overdue || (i.Status == FeedStatus.Upcoming && i.At is { } at && at - now <= TimeSpan.FromMinutes(30));

        /// <summary>The ones with a countdown first (soonest on top), then late ones, then the living, then unknown.</summary>
        private IEnumerable<FeedItem> Shown() => (tab switch
        {
            "alive" => visible.Where(i => i.Status == FeedStatus.Alive),
            "soon" => visible.Where(Soon),
            _ => visible,
        }).OrderBy(i => i.Status switch { FeedStatus.Overdue => 0, FeedStatus.Upcoming => 1, FeedStatus.Alive => 2, _ => 3 })
          .ThenBy(i => i.Status == FeedStatus.Alive ? DateTimeOffset.MaxValue - (i.At ?? DateTimeOffset.MinValue) : TimeSpan.Zero)
          .ThenBy(i => i.Status == FeedStatus.Upcoming ? i.At ?? DateTimeOffset.MaxValue : DateTimeOffset.MaxValue);

        /// <summary>"с 01:32" under a living boss: when it appeared.</summary>
        private static string? AliveSince(FeedItem i) =>
            i.Status == FeedStatus.Alive && i.At is { } since ? "с " + TimeZoneInfo.ConvertTime(since, TimeZoneInfo.Local).ToString("HH:mm") : null;

        private string TimeText(FeedItem i) => i.Status switch
        {
            FeedStatus.Alive => "жив",
            FeedStatus.Upcoming when i.At is { } at => FeedText.Span(at - now),
            FeedStatus.Overdue => "скоро",
            _ => "—",
        };

        /// <summary>"Name (code)" from the list, or a name only one boss has.</summary>
        private void Assign(int place, string? text)
        {
            naming = 0;
            if (string.IsNullOrWhiteSpace(text)) return;
            var m = System.Text.RegularExpressions.Regex.Match(text, @"\((\d+)\)\s*$");
            var code = m.Success ? int.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture) : 0;
            if (code == 0 && catalog.Bosses().Where(b => b.Name.Equals(text.Trim(), StringComparison.CurrentCultureIgnoreCase)).ToList() is { Count: 1 } one)
                code = one[0].Code;
            if (code > 0) tracker.AssignSlot(place, code);
            Recompute();
        }

        public void Dispose()
        {
            controller.EditingChanged -= OnEditingChanged;
            cts?.Cancel();
            cts?.Dispose();
        }
    }
}
