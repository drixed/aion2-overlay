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
using AionDpsMeter.UI.Utils;
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
        AionDpsMeter.Timers.Modes.MeterModesService modes,
        AionDpsMeter.Timers.Pets.PetTracker pets,
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

        /// <summary>"+4.2%" on my own row: my DPS now against my best on this boss, once the DPS settles.</summary>
        private string? VsBest()
        {
            if (FightRecord is not { BestMyDps: > 0 } r || Fight is not { } f || f.Elapsed < BossFight.MinimumSample) return null;
            var mine = sessions.PlayerStats.FirstOrDefault(p => p.IsUser)?.DamagePerSecond ?? 0;
            return mine > 0 ? AionDpsMeter.Timers.Records.RecordText.VsBest(mine, r.BestMyDps) : null;
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
        private IReadOnlyList<EventChip> Chips = [];
        private bool modeMenuOpen;
        private (AionDpsMeter.Timers.Pets.PetView Pet, string Name)? PetLine;

        /// <summary>The pet line while hitting a monster whose pet is not maxed: its name (or the monster's) on the left,
        /// "ур. 2 · 13 / 75" on the right — the numbers never get cut off.</summary>
        private string PetName((AionDpsMeter.Timers.Pets.PetView Pet, string Name) line) =>
            schedule.Current.PetName(line.Pet.Pet) ?? line.Name; // names live in schedule.json

        private static string PetStat(AionDpsMeter.Timers.Pets.PetView p) =>
            $"{(p.Level == 0 ? "сбор" : $"ур. {p.Level}")} · {(p.Souls is { } s ? $"{s} / {p.Need}" : $"? / {p.Need}")}";

        private string PetTitle((AionDpsMeter.Timers.Pets.PetView Pet, string Name) line) =>
            $"{PetName(line)} — души падают с «{line.Name}». «?» — число придёт при следующем входе персонажа. Все питомцы — в настройках, вкладка «Питомцы»";

        /// <summary>"A pet reached level 2" for half a minute.</summary>
        private string? PetNews() =>
            Options.ShowPets && pets.Latest is { } n && now - n.At <= RecordNewsFor
                ? $"{schedule.Current.PetName(n.Pet) ?? n.Name}: уровень {n.Level}!" : null;
        private long? recapPlayer;

        /// <summary>The skull on a row opens (or closes) that player's last death recap.</summary>
        private void ToggleRecap(long playerId) => recapPlayer = recapPlayer == playerId ? null : playerId;

        private string RecapName(long playerId) =>
            ViewModel?.Players.FirstOrDefault(p => p.PlayerId == playerId)?.PlayerNameDisplay
            ?? entities.GetPlayerEntity((int)playerId)?.Name
            ?? $"Игрок {playerId}";
        private bool targetIsDummy;

        private static readonly AionDpsMeter.Timers.Modes.MeterMode[] AllModes = Enum.GetValues<AionDpsMeter.Timers.Modes.MeterMode>();

        /// <summary>The mode in effect: hitting a training scarecrow shows the dummy run (when the user asked for that).</summary>
        private AionDpsMeter.Timers.Modes.MeterMode Mode =>
            Options.AutoDummy && targetIsDummy && Options.Mode is AionDpsMeter.Timers.Modes.MeterMode.Dps or AionDpsMeter.Timers.Modes.MeterMode.Damage ? AionDpsMeter.Timers.Modes.MeterMode.Dummy : Options.Mode;

        private static string ModeLabel(AionDpsMeter.Timers.Modes.MeterMode mode) => mode switch
        {
            AionDpsMeter.Timers.Modes.MeterMode.Damage => "Урон",
            AionDpsMeter.Timers.Modes.MeterMode.Heal => "Хил",
            AionDpsMeter.Timers.Modes.MeterMode.Dummy => "Манекен",
            AionDpsMeter.Timers.Modes.MeterMode.Grind => "Гринд",
            _ => "DPS",
        };

        private static string ModeHint(AionDpsMeter.Timers.Modes.MeterMode mode) => mode switch
        {
            AionDpsMeter.Timers.Modes.MeterMode.Damage => "Общий урон за бой",
            AionDpsMeter.Timers.Modes.MeterMode.Heal => "Лечение других игроков за бой (HPS)",
            AionDpsMeter.Timers.Modes.MeterMode.Dummy => "Замер на тренировочном чучеле фиксированной длины",
            AionDpsMeter.Timers.Modes.MeterMode.Grind => "Урон по всем целям между «Старт» и «Стоп»",
            _ => "Урон в секунду",
        };

        private bool AltMode => Mode is AionDpsMeter.Timers.Modes.MeterMode.Heal or AionDpsMeter.Timers.Modes.MeterMode.Dummy or AionDpsMeter.Timers.Modes.MeterMode.Grind;

        private void PickMode(AionDpsMeter.Timers.Modes.MeterMode mode)
        {
            modeMenuOpen = false;
            options.Current.Mode = mode;
            options.Save();
        }

        private void ToggleModeMenu() => modeMenuOpen = !modeMenuOpen;

        private string GrindButton => modes.Grind.Running ? "■ Стоп" : "▶ Старт";

        private void ToggleGrind()
        {
            if (modes.Grind.Running) modes.Grind.Stop(DateTime.Now);
            else modes.Grind.Play(DateTime.Now);
        }

        private sealed record AltRow(long PlayerId, string Name, string ClassId, bool IsUser, long Total, double PerSecond, double Share, double Bar);

        /// <summary>Rows of the Heal, Dummy and Grind modes, named from the meter's players (or the entity table).</summary>
        private IReadOnlyList<AltRow> AltRows()
        {
            var at = DateTime.Now;
            IReadOnlyList<AionDpsMeter.Timers.Modes.ModeRow> rows = Mode switch
            {
                AionDpsMeter.Timers.Modes.MeterMode.Heal when sessions.GetCombatDuration() is var d && d > TimeSpan.Zero => modes.Heals.Rows(at - d, at),
                AionDpsMeter.Timers.Modes.MeterMode.Dummy => modes.Dummy.View(at).Rows,
                AionDpsMeter.Timers.Modes.MeterMode.Grind => modes.Grind.View(at).Rows,
                _ => [],
            };
            if (rows.Count == 0) return [];
            var known = ViewModel!.Players.GroupBy(p => p.PlayerId).ToDictionary(g => g.Key, g => g.First());
            double sum = rows.Sum(r => r.Total), max = rows.Max(r => r.Total);
            return rows.Select(r =>
            {
                known.TryGetValue(r.PlayerId, out var p);
                var e = p is null ? entities.GetPlayerEntity((int)r.PlayerId) : null;
                var name = p?.PlayerNameDisplay ?? (string.IsNullOrEmpty(e?.Name) ? $"Игрок {r.PlayerId}" : e!.Name);
                var classId = p?.ClassId ?? e?.CharacterClass?.Id.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "";
                return new AltRow(r.PlayerId, name, classId, p?.IsUser ?? e?.IsUser ?? false,
                    r.Total, r.PerSecond, sum > 0 ? r.Total * 100 / sum : 0, max > 0 ? r.Total * 100 / max : 0);
            }).ToList();
        }

        /// <summary>Clicking a name in the Heal, Dummy and Grind modes opens the same skill breakdown as in DPS mode
        /// (the player's current fight); a player the fight does not have opens nothing.</summary>
        private void OpenAltDetails(AltRow r)
        {
            if (ViewModel!.Players.FirstOrDefault(p => p.PlayerId == r.PlayerId) is { } player) ViewModel.OpenPlayerDetails(player);
        }

        private string AltRowClass(AltRow r) => ViewModel!.GetPlayerRowClass(new PlayerRenderState { ClassId = r.ClassId, IsUser = r.IsUser });

        /// <summary>The line under the header in the Dummy and Grind modes.</summary>
        private string? ModeStatus()
        {
            var at = DateTime.Now;
            switch (Mode)
            {
                case AionDpsMeter.Timers.Modes.MeterMode.Dummy:
                    var v = modes.Dummy.View(at);
                    return !v.Started ? $"🎯 Ударь тренировочное чучело — замер {FeedText.Span(modes.Dummy.Length)} начнётся с первого удара"
                        : v.Finished ? $"🎯 Итог за {FeedText.Span(v.Elapsed)} · новый замер — после паузы 5 с"
                        : $"🎯 Замер: осталось {FeedText.Span(v.Left)}";
                case AionDpsMeter.Timers.Modes.MeterMode.Grind:
                    var g = modes.Grind.View(at);
                    return g.Running ? $"⛏ Гринд идёт {FeedText.Span(g.Elapsed)}"
                        : g.Elapsed > TimeSpan.Zero ? $"⛏ Гринд остановлен: {FeedText.Span(g.Elapsed)}"
                        : "⛏ Нажми «Старт»: урон по всем целям копится до «Стоп»";
                default:
                    return null;
            }
        }
        private EnergyState? Energy => energy.Current;
        private TimersOptions Options => options.Current;
        private Dictionary<long, (double Effective, double Active)> dpsCache = new();
        private DateTimeOffset dpsCacheAt;


        /// <summary>Compact bar: header and my own row only.</summary>
        private bool Compact => Options.Compact || (Options.AutoCompact && Fight is null && ViewModel?.HasActiveTarget != true);

        /// <summary>Theme and bar style as classes on the panel; the CSS does the rest.</summary>
        private string PanelClasses =>
            $"bp-theme-{Options.Theme.ToString().ToLowerInvariant()} bp-style-{Options.BarStyle.ToString().ToLowerInvariant()}";

        /// <summary>Font, size, bar height, accent and background as CSS variables.</summary>
        private string PanelStyle
        {
            get
            {
                var font = Options.Font switch
                {
                    AionDpsMeter.Timers.Overlay.PanelFont.Narrow => "'Bahnschrift SemiCondensed', Bahnschrift, 'Segoe UI', sans-serif",
                    AionDpsMeter.Timers.Overlay.PanelFont.Mono => "Consolas, 'Cascadia Mono', monospace",
                    AionDpsMeter.Timers.Overlay.PanelFont.Wide => "Verdana, 'Segoe UI', sans-serif",
                    _ => "'Segoe UI', sans-serif",
                };
                var style = $"--bp-font:{font};--bp-fs:{Math.Clamp(Options.FontSize, 10, 16)}px;" +
                            $"--bp-rh:{(Math.Clamp(Options.BarHeight, 60, 160) / 100.0).ToString(System.Globalization.CultureInfo.InvariantCulture)};";
                if (IsColor(Options.AccentColor)) style += $"--bp-accent:{Options.AccentColor};";
                if (IsColor(Options.BackgroundColor)) style += $"--bp-bg:{Options.BackgroundColor}d9;"; // 85% so the game shows through
                return style;
            }
        }

        private static bool IsColor(string? value) => value is { Length: 7 } && value[0] == '#' && value[1..].All(Uri.IsHexDigit);

        /// <summary>Rows in display order (my row on top, first place at the bottom — as set) with their rank by damage.</summary>
        private IReadOnlyList<(PlayerRenderState Player, double Share, double Bar, int Rank)> OrderedRows() =>
            AionDpsMeter.Timers.Overlay.RowOrder.Apply(Rows().ToList(), r => r.Player.IsUser, Options.KeepMeOnTop, Options.FirstPlaceAtBottom)
                .Select(x => (x.Row.Player, x.Row.Share, x.Row.Bar, x.Rank)).ToList();

        private IReadOnlyList<(AltRow Row, int Rank)> OrderedAltRows() =>
            AionDpsMeter.Timers.Overlay.RowOrder.Apply(AltRows(), r => r.IsUser, Options.KeepMeOnTop, Options.FirstPlaceAtBottom);
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
            if (Math.Abs(window.Height - target) < 1) return;
            if (Options.GrowUpward) window.Top -= target - window.Height; // the bottom edge stays where it is
            window.Height = target;
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
                targetIsDummy = target?.IsDummy == true;
                PetLine = Options.ShowPets ? pets.Current() : null;
                Fight = target is { HpTotal: > 0, HpCurrent: > 0 } && (fightIsBoss || options.Current.CardForAllTargets)
                    ? BossFight.Build(target.MobCode == 0 ? "Цель" : target.Name, target.HpCurrent, target.HpTotal, sessions.GetPartyDps(),
                        sessions.GetCombatDuration(),
                        BossFight.EnrageFor(target.MobCode, fightIsBoss, tracker.IsFieldBoss(target.MobCode), schedule.Current.Enrage))
                    : null;
                FightRecord = Fight is not null && fightIsBoss && Options.ShowRecords ? records.For(target!.Name, target.HpTotal) : null;
                // One icon per event (rift, shugo, air raid, arena, reset), minus the ones hidden in settings.
                var shown = TimersFeed.BuildSchedule(now, schedule.Current).Where(options.Current.Shows).Select(i => i.Id).ToHashSet();
                Chips = EventChips.Build(now, schedule.Current, alwaysTime: options.Current.EventTimesAlways).Where(c => shown.Contains(c.Id)).ToList();
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
