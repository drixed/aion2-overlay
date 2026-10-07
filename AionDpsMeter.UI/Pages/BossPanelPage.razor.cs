// aion2-overlay fork: new file (see FORK_CHANGES.md).
using AionDpsMeter.Services.Services.Session;
using AionDpsMeter.Timers.Bosses;
using AionDpsMeter.Timers.Feed;
using AionDpsMeter.Timers.Fight;
using AionDpsMeter.Timers.Schedule;
using AionDpsMeter.UI.ViewModels;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace AionDpsMeter.UI.Pages
{
    /// <summary>Main window in the "boss fight" style: our header and boss card, upstream's player rows, next rift below.</summary>
    public partial class BossPanelPage(
        CombatSessionManager sessions,
        FieldBossTracker tracker,
        ScheduleSource schedule,
        TimersOptionsStore options,
        TimeProvider time,
        ILogger<BossPanelPage> logger) : ComponentBase
    {
        [Parameter]
        public MainDpsViewModel? ViewModel { get; set; }

        private BossFightView? Fight;
        private bool fightIsBoss;
        private bool noticeOpen;
        private string? snoozedNotice; // «Напомнить позже»: hidden until the app restarts

        private Notice? ActiveNotice =>
            schedule.Current.Notice is { } n && !options.Current.IsDismissed(n.Id) && snoozedNotice != n.Id ? n : null;
        private FeedItem? NextEvent;
        private DateTimeOffset now;
        private CancellationTokenSource? cts;
        private bool loggedFailure;

        private string HeaderTitle =>
            Fight is not null && fightIsBoss ? "Бой с боссом"
            : Fight is not null ? Fight.Name
            : "Ожидание боя";

        protected override void OnInitialized()
        {
            cts = new CancellationTokenSource();
            _ = TickAsync(cts.Token); // the footer countdown runs even when upstream has nothing new to draw
        }

        protected override void OnParametersSet() => Recompute();

        private async Task TickAsync(CancellationToken ct)
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
            try
            {
                while (await timer.WaitForNextTickAsync(ct))
                {
                    Recompute();
                    await InvokeAsync(StateHasChanged);
                }
            }
            catch (OperationCanceledException) { }
        }

        private void Recompute()
        {
            now = time.GetUtcNow();
            try
            {
                // Any target with HP gets the card (upstream's mob table does not flag every boss); the "boss fight"
                // title and the enrage timer are for bosses only.
                var target = sessions.GetActiveTargetInfo();
                fightIsBoss = target is { IsBoss: true, IsDummy: false };
                Fight = target is { HpTotal: > 0 }
                    ? BossFight.Build(target.MobCode == 0 ? "Цель" : target.Name, target.HpCurrent, target.HpTotal, sessions.GetPartyDps(),
                        sessions.GetCombatDuration(),
                        BossFight.EnrageFor(target.MobCode, fightIsBoss, tracker.IsFieldBoss(target.MobCode), schedule.Current.Enrage))
                    : null;
                NextEvent = TimersFeed.BuildSchedule(now, schedule.Current).FirstOrDefault(options.Current.Shows);
            }
            catch (Exception ex)
            {
                Fight = null; // never break upstream's window
                if (!loggedFailure) logger.LogError(ex, "Boss panel could not read the fight");
                loggedFailure = true;
            }
        }

        private static string Hp(long value) => DamageFormatter.Format(value);

        /// <summary>The party total only adds information when more than one player is in the list.</summary>
        private bool ShowPartyDps => ViewModel is { Players.Count: > 1 };

        private static IEnumerable<string> Paragraphs(string text) =>
            text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        private void ToggleNotice() => noticeOpen = !noticeOpen;

        private void RemindLater()
        {
            snoozedNotice = ActiveNotice?.Id;
            noticeOpen = false;
        }

        private void DismissNotice()
        {
            if (ActiveNotice is { } n)
            {
                options.Current.DismissedNotices.Add(n.Id);
                options.Save();
            }
            noticeOpen = false;
        }

        public void Dispose()
        {
            cts?.Cancel();
            cts?.Dispose();
        }
    }
}
