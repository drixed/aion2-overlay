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
        private FeedItem? NextEvent;
        private DateTimeOffset now;
        private CancellationTokenSource? cts;
        private bool loggedFailure;

        private string HeaderTitle =>
            Fight is not null ? "Бой с боссом"
            : ViewModel?.HasActiveTarget == true && ViewModel.ActiveTargetName.Length > 0 ? ViewModel.ActiveTargetName
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
                var target = sessions.GetActiveTargetInfo();
                Fight = target is { IsBoss: true, IsDummy: false, HpTotal: > 0 }
                    ? BossFight.Build(target.Name, target.HpCurrent, target.HpTotal, sessions.GetPartyDps(),
                        sessions.GetCombatDuration(),
                        BossFight.EnrageFor(target.MobCode, tracker.IsFieldBoss(target.MobCode), schedule.Current.Enrage))
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

        public void Dispose()
        {
            cts?.Cancel();
            cts?.Dispose();
        }
    }
}
