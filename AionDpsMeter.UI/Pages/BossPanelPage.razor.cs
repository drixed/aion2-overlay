// aion2-overlay fork: new file (see FORK_CHANGES.md).
using AionDpsMeter.Services.Services.Entity;
using AionDpsMeter.Services.Services.Session;
using AionDpsMeter.Timers.Bosses;
using AionDpsMeter.Timers.Energy;
using AionDpsMeter.Timers.Feed;
using AionDpsMeter.Timers.Fight;
using AionDpsMeter.Timers.Overlay;
using AionDpsMeter.Timers.Schedule;
using AionDpsMeter.UI.Services.TimerWindow;
using AionDpsMeter.UI.ViewModels;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
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
        EnergyTracker energy,
        EntityTracker entities,
        CubeMapWindow cubeMap,
        AlertRunner alertRunner,
        AionDpsMeter.Timers.Records.BossRecords records,
        IJSRuntime js,
        IServiceProvider services,
        ILogger<BossPanelPage> logger) : ComponentBase
    {
        [Parameter]
        public MainDpsViewModel? ViewModel { get; set; }

        private BossFightView? Fight;
        private AionDpsMeter.Timers.Records.BossRecord? FightRecord;
        private static readonly TimeSpan RecordNewsFor = TimeSpan.FromSeconds(30);

        /// <summary>The record line under the boss card: best kill, own best DPS, and the pace once it can be estimated.</summary>
        private (string Text, string? Pace, bool Faster)? RecordLine(BossFightView f)
        {
            if (FightRecord is not { } r) return null;
            var text = $"Рекорд {AionDpsMeter.Timers.Records.RecordText.Time(r.BestKill)}" + (r.BestMyDps > 0 ? $" · твой {DamageFormatter.Format(r.BestMyDps)}/s" : "");
            var delta = AionDpsMeter.Timers.Records.BossPace.Delta(r, f.Elapsed, f.ToKill);
            return (text, delta is { } d ? AionDpsMeter.Timers.Records.RecordText.Pace(d) : null, delta is { } d2 && d2 < TimeSpan.Zero);
        }

        /// <summary>"New record" for half a minute after the kill.</summary>
        private string? RecordNews()
        {
            if (!Options.ShowRecords || records.Latest is not { } n || now - n.At > RecordNewsFor) return null;
            var boss = n.Record.Boss;
            return n.News == AionDpsMeter.Timers.Records.RecordNews.FasterKill
                ? $"Новый рекорд: {boss} за {AionDpsMeter.Timers.Records.RecordText.Time(n.Record.BestKill)}"
                  + (n.Record.PreviousBestKill is { } p ? $" (−{AionDpsMeter.Timers.Records.RecordText.Time(p - n.Record.BestKill)})" : "")
                : $"Твой лучший DPS на {boss}: {DamageFormatter.Format(n.MyDps)}/s";
        }
        private bool fightIsBoss;
        private bool noticeOpen;
        private string? snoozedNotice; // «Напомнить позже»: hidden until the app restarts

        private Notice? ActiveNotice =>
            schedule.Current.Notice is { } n && !options.Current.IsDismissed(n.Id) && snoozedNotice != n.Id ? n : null;
        private FeedItem? NextRift;
        private FeedItem? NextEvent;
        private EnergyState? Energy => energy.Current;
        private TimersOptions Options => options.Current;
        private Dictionary<long, (double Effective, double Active)> dpsCache = new();
        private DateTimeOffset dpsCacheAt;


        /// <summary>Compact bar: header and my own row only.</summary>
        private bool Compact => Options.Compact;
        private void ToggleCubeMap() => cubeMap.Toggle();
        private string? AlertText => alertRunner.ActiveText;

        /// <summary>Upstream's player stats are rebuilt on every read: refreshed at most twice a second, only when
        /// aDPS or the party filter needs them.</summary>
        private Dictionary<long, (double Effective, double Active)> Dps()
        {
            if (now - dpsCacheAt > TimeSpan.FromMilliseconds(500))
            {
                dpsCacheAt = now;
                try
                {
                    dpsCache = sessions.PlayerStats.ToDictionary(p => p.PlayerId,
                        p => (p.DamagePerSecond, DpsMath.Active(p.TotalDamage, p.FirstHit, p.LastHit)));
                }
                catch (Exception) { }
            }
            return dpsCache;
        }

        private string DpsOf(PlayerRenderState player) =>
            Options.DpsMode == DpsMode.Active && Dps().TryGetValue(player.PlayerId, out var dps)
                ? DamageFormatter.Format(dps.Active)
                : player.DpsFormatted;

        /// <summary>БМ, ГС, both or nothing next to the name, as picked in settings.</summary>
        private string PowerOf(PlayerRenderState player)
        {
            int gearScore;
            try { gearScore = entities.GetPlayerEntity((int)player.PlayerId)?.GearScore ?? 0; }
            catch (Exception) { gearScore = 0; }
            return PowerText.Format(Options.PowerColumn, player.CombatPower, gearScore);
        }

        private int LevelOf(long playerId)
        {
            try { return entities.GetPlayerEntity((int)playerId)?.CharacterLevel ?? 0; }
            catch (Exception) { return 0; }
        }

        /// <summary>The rows to show with their share and bar: everyone (upstream's numbers) or only my party
        /// (recomputed within it).</summary>
        private IEnumerable<(PlayerRenderState Player, double Share, double Bar)> Rows()
        {
            var players = ViewModel!.Players;
            if (Options.Compact)
                return players.Where(p => p.IsUser).Select(p => (p, p.DamagePercentage, ViewModel.ClampPercent(p.EffectivePercentage)));
            if (!Options.OnlyMyParty)
                return players.Select(p => (p, p.DamagePercentage, ViewModel.ClampPercent(p.EffectivePercentage)));
            var rows = players.Select(p => new PartyRow(p.PlayerId, p.IsUser, LevelOf(p.PlayerId), p.TotalDamage)).ToList();
            var shares = PartyFilter.Shares(rows);
            var bars = PartyFilter.Bars(rows);
            return players.Where(p => shares.ContainsKey(p.PlayerId)).Select(p => (p, shares[p.PlayerId], bars[p.PlayerId]));
        }

        private string PartyDpsText
        {
            get
            {
                if (!Options.OnlyMyParty && Options.DpsMode == DpsMode.Effective) return ViewModel!.TotalRaidDamageFormatted;
                var dps = Dps();
                var total = Rows().Sum(r => dps.TryGetValue(r.Player.PlayerId, out var d)
                    ? (Options.DpsMode == DpsMode.Active ? d.Active : d.Effective) : 0);
                return $"{DamageFormatter.Format(total)}/s";
            }
        }

        private DateTimeOffset now;
        private CancellationTokenSource? cts;
        private bool loggedFailure;

        private string HeaderTitle =>
            Fight is not null && fightIsBoss ? "Бой с боссом"
            : Fight is not null ? Fight.Name
            : ViewModel?.HasActiveTarget == true && ViewModel.ActiveTargetName.Length > 0 && !ViewModel.ActiveTargetName.StartsWith("Unknown") ? ViewModel.ActiveTargetName
            : "Ожидание боя";

        protected override void OnInitialized()
        {
            cts = new CancellationTokenSource();
            _ = TickAsync(cts.Token); // the footer countdown runs even when upstream has nothing new to draw
        }

        protected override void OnParametersSet() => Recompute();

        private ElementReference panel;
        private DotNetObjectReference<BossPanelPage>? self;
        private const double MaxWindowHeight = 900;
        private const double BottomSlack = 4; // keeps the last line off the window's edge (and the resize grip)

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (!firstRender || ViewModel is null) return;
            self = DotNetObjectReference.Create(this);
            try
            {
                await js.InvokeVoidAsync("aionFit.observe", panel, self);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Main window will not fit its content"); // the window just keeps its size
            }
        }

        /// <summary>The panel's natural height changed (card shown, players joined): the main window follows it,
        /// like the Abyss meter does. Width stays the user's.</summary>
        [JSInvokable]
        public void OnContentHeight(double height)
        {
            var window = services.GetService<Views.MainWindow>();
            if (window is null || height <= 0) return;
            var target = Math.Clamp(height + BottomSlack, window.MinHeight, MaxWindowHeight);
            if (Math.Abs(window.Height - target) >= 1) window.Height = target;
        }

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
                // The card is for bosses (ordinary mobs too when the user asks for it); a dead target drops it.
                var target = sessions.GetActiveTargetInfo();
                fightIsBoss = target is { IsBoss: true, IsDummy: false };
                Fight = target is { HpTotal: > 0, HpCurrent: > 0 } && (fightIsBoss || options.Current.CardForAllTargets)
                    ? BossFight.Build(target.MobCode == 0 ? "Цель" : target.Name, target.HpCurrent, target.HpTotal, sessions.GetPartyDps(),
                        sessions.GetCombatDuration(),
                        BossFight.EnrageFor(target.MobCode, fightIsBoss, tracker.IsFieldBoss(target.MobCode), schedule.Current.Enrage))
                    : null;
                FightRecord = Fight is not null && fightIsBoss && Options.ShowRecords ? records.For(target!.Name, target.HpTotal) : null;
                // The rift and the next other event each get a line, so hourly events never push the rift out.
                var upcoming = TimersFeed.BuildSchedule(now, schedule.Current).Where(options.Current.Shows).ToList();
                NextRift = upcoming.FirstOrDefault(i => i.Kind == FeedKind.Rift);
                NextEvent = upcoming.FirstOrDefault(i => i.Kind != FeedKind.Rift);
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
        private bool ShowPartyDps => ViewModel is not null && Rows().Count() > 1;

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
            self?.Dispose();
            cts?.Cancel();
            cts?.Dispose();
        }
    }
}
