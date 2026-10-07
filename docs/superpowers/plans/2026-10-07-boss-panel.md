# Панель «Бой с боссом» и русский интерфейс — план реализации

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Главное окно в стиле «Бой с боссом» (HP, до победы, до ярости, разлом внизу), окно таймеров только для полевых боссов (с изменением размера и подсказкой), русский текст в Blazor-окнах RATmeter.

**Architecture:** Логика боя и ленты — в `AionDpsMeter.Timers` (xUnit). Новая страница `BossPanelPage` в UI встраивает `MainDpsMinimal` апстрима для строк игроков и выбирается настройкой `MainStyle` (+3 строки в `MainDpsPage.razor`). Перевод — `wwwroot/js/ru.js` (+1 строка в `index.html`).

**Tech Stack:** C# / .NET 10, Blazor WebView в WPF, xUnit, JavaScript (MutationObserver).

**Spec:** `docs/superpowers/specs/2026-10-07-boss-panel-design.md` (дополняет `2026-10-07-aion2-overlay-design.md`)

## Global Constraints

- Все правила `docs/superpowers/plans/2026-10-07-aion2-overlay.md` (Global Constraints) в силе: минимум правок апстрима, каждая — в `FORK_CHANGES.md`; новые файлы в проектах апстрима — с пометкой `aion2-overlay fork`; сбои нашего кода не роняют окна апстрима.
- Таймер ярости по умолчанию — 5:00; полевые боссы — без ярости; `enrageSeconds` в `schedule.json` переопределяет (0 — нет ярости).
- Коммиты заканчиваются строкой `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.
- `dotnet`: `export PATH="$PATH:/c/Program Files/dotnet"`.

## Review Focus

1. **Нет цели / цель не босс / манекен** — главное окно показывает «Ожидание боя» или имя цели, карточки нет, ничего не падает (`BossPanelPage.Recompute` в try/catch; манекен исключён).
2. **ДПС = 0 или HP = 0 в начале/конце боя** — «До победы» = «—», без деления на ноль (тест в Task 1).
3. **Окно вне боя** — разлом в подвале тикает и без обновлений от апстрима (свой таймер 1 с в `BossPanelPage`).
4. **Перевод не зацикливается** — замена текста вызывает мутацию, но русский текст не находится в словаре (проверка в Task 5).
5. **Апстрим переименовал шапку `MainDpsMinimal`** — появится вторая шапка, но окно работает (принято в спецификации).

---

### Task 1: Ярость и «до победы» — логика

**Files:**
- Modify: `AionDpsMeter.Timers/Schedule/ScheduleData.cs`, `AionDpsMeter.Timers/Schedule/ScheduleJson.cs`, `AionDpsMeter.Timers/Bosses/FieldBossTracker.cs`
- Create: `AionDpsMeter.Timers/Fight/BossFight.cs`
- Test: `AionDpsMeter.Timers.Tests/Fight/BossFightTests.cs`; добавить тесты в `Schedule/ScheduleJsonTests.cs`, `Bosses/FieldBossTrackerTests.cs`

**Interfaces:**
- Produces:
  - `ScheduleData(..., IReadOnlyDictionary<int,int>? EnrageSeconds = null)` + свойство `IReadOnlyDictionary<int,int> Enrage`
  - `bool FieldBossTracker.IsFieldBoss(int code)`
  - `record BossFightView(string Name, long HpCurrent, long HpTotal, double HpPercent, TimeSpan Elapsed, TimeSpan? ToKill, TimeSpan? Enrage, TimeSpan? ToEnrage)` с `bool Enraged`
  - `static TimeSpan? BossFight.EnrageFor(int mobCode, bool isFieldBoss, IReadOnlyDictionary<int,int> overrides)`, `static BossFightView BossFight.Build(string name, long hpCurrent, long hpTotal, double partyDps, TimeSpan elapsed, TimeSpan? enrage)`, `static readonly TimeSpan BossFight.DefaultEnrage`

- [ ] **Step 1: Write the failing tests**

`AionDpsMeter.Timers.Tests/Fight/BossFightTests.cs`:

```csharp
using AionDpsMeter.Timers.Fight;

namespace AionDpsMeter.Timers.Tests.Fight;

public class BossFightTests
{
    private static readonly IReadOnlyDictionary<int, int> NoOverrides = new Dictionary<int, int>();

    [Fact]
    public void Time_to_kill_is_remaining_hp_over_party_dps()
    {
        var f = BossFight.Build("Pinopi", 650_000, 1_200_000, 10_000, TimeSpan.FromSeconds(57), null);
        Assert.Equal(TimeSpan.FromSeconds(65), f.ToKill);
        Assert.InRange(f.HpPercent, 54.16, 54.17);
    }

    [Fact]
    public void No_dps_no_hp_or_an_absurd_estimate_shows_nothing()
    {
        Assert.Null(BossFight.Build("B", 650_000, 1_200_000, 0, TimeSpan.Zero, null).ToKill);
        Assert.Null(BossFight.Build("B", 0, 1_200_000, 10_000, TimeSpan.Zero, null).ToKill);
        Assert.Null(BossFight.Build("B", 1_000_000_000, 1_000_000_000, 1, TimeSpan.Zero, null).ToKill);
        Assert.Equal(0, BossFight.Build("B", 5, 0, 1, TimeSpan.Zero, null).HpPercent);
    }

    [Fact]
    public void Enrage_counts_down_from_the_fight_start()
    {
        var f = BossFight.Build("B", 1, 2, 1, TimeSpan.FromSeconds(57), BossFight.DefaultEnrage);
        Assert.Equal(new TimeSpan(0, 4, 3), f.ToEnrage); // the screenshot: 0:57 into the fight, 4:03 to enrage
        Assert.False(f.Enraged);

        var late = BossFight.Build("B", 1, 2, 1, TimeSpan.FromSeconds(310), BossFight.DefaultEnrage);
        Assert.Equal(TimeSpan.Zero, late.ToEnrage);
        Assert.True(late.Enraged);
    }

    [Fact]
    public void Enrage_timer_comes_from_overrides_then_boss_kind()
    {
        var overrides = new Dictionary<int, int> { [2900001] = 420, [2900002] = 0 };
        Assert.Equal(TimeSpan.FromMinutes(7), BossFight.EnrageFor(2900001, isFieldBoss: false, overrides));
        Assert.Null(BossFight.EnrageFor(2900002, isFieldBoss: false, overrides));
        Assert.Null(BossFight.EnrageFor(2400800, isFieldBoss: true, NoOverrides));
        Assert.Equal(TimeSpan.FromMinutes(5), BossFight.EnrageFor(2900003, isFieldBoss: false, NoOverrides));
    }
}
```

В `ScheduleJsonTests` добавить:

```csharp
    [Fact]
    public void Enrage_overrides_parse_and_zero_means_none()
    {
        var data = ScheduleJson.Parse("""{ "enrageSeconds": { "2400800": 0, "2900001": 420, "x": 5, "2900002": -1 } }""");
        Assert.Equal(0, data.Enrage[2400800]);
        Assert.Equal(420, data.Enrage[2900001]);
        Assert.Equal(2, data.Enrage.Count);
        Assert.Empty(ScheduleData.Empty.Enrage);
    }
```

В `FieldBossTrackerTests` добавить:

```csharp
    [Fact]
    public void IsFieldBoss_follows_known_and_learned_maps()
    {
        var t = Tracker();
        Assert.True(t.IsFieldBoss(2400800));
        Assert.False(t.IsFieldBoss(2500001));
        t.OnList(1, List(77, 2, Slot(77, 1, true, time.GetUtcNow()), Slot(77, 2, true, time.GetUtcNow())));
        t.OnKill(1, 2500002);
        Assert.True(t.IsFieldBoss(2500001));
    }
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test AionDpsMeter.Timers.Tests`
Expected: FAIL — компиляция: нет `AionDpsMeter.Timers.Fight`, `ScheduleData.Enrage`, `IsFieldBoss`.

- [ ] **Step 3: Write the implementation**

`ScheduleData.cs` — заменить record:

```csharp
public sealed record ScheduleData(
    IReadOnlyList<ScheduledEvent> Events,
    IReadOnlyDictionary<int, FieldBossMapInfo> FieldBossMaps,
    IReadOnlyDictionary<int, int> RespawnMinutes,
    IReadOnlyDictionary<int, int>? EnrageSeconds = null)
{
    private static readonly IReadOnlyDictionary<int, int> None = new Dictionary<int, int>();

    public static ScheduleData Empty { get; } =
        new([], new Dictionary<int, FieldBossMapInfo>(), new Dictionary<int, int>());

    /// <summary>NPC code → enrage timer in seconds; 0 = the boss has none.</summary>
    public IReadOnlyDictionary<int, int> Enrage => EnrageSeconds ?? None;
}
```

`ScheduleJson.cs` — в `FileDto` добавить `public Dictionary<string, int>? EnrageSeconds { get; set; }`; в `Parse` перед `return`:

```csharp
        var enrage = new Dictionary<int, int>();
        foreach (var (key, seconds) in file.EnrageSeconds ?? new())
            if (int.TryParse(key, out var code) && seconds >= 0) enrage[code] = seconds;

        return new ScheduleData(events, maps, respawn, enrage);
```

`FieldBossTracker.cs` — публичный метод рядом с `RespawnMinutesOf`:

```csharp
    /// <summary>A boss of a field map (known from schedule.json or learned): it respawns and has no enrage timer.</summary>
    public bool IsFieldBoss(int code)
    {
        lock (gate) return IsFieldBlock(code / 1000);
    }
```

`AionDpsMeter.Timers/Fight/BossFight.cs`:

```csharp
namespace AionDpsMeter.Timers.Fight;

/// <param name="ToKill">Remaining HP over the party's DPS; null when it cannot be estimated.</param>
/// <param name="Enrage">The boss's enrage timer; null when it has none.</param>
public sealed record BossFightView(
    string Name,
    long HpCurrent,
    long HpTotal,
    double HpPercent,
    TimeSpan Elapsed,
    TimeSpan? ToKill,
    TimeSpan? Enrage,
    TimeSpan? ToEnrage)
{
    public bool Enraged => Enrage is { } e && Elapsed >= e;
}

public static class BossFight
{
    /// <summary>Most dungeon bosses go berserk after 5:00 of combat (metabot.gg boss pages).</summary>
    public static readonly TimeSpan DefaultEnrage = TimeSpan.FromMinutes(5);

    private static readonly double MaxShownSeconds = (TimeSpan.FromMinutes(100) - TimeSpan.FromSeconds(1)).TotalSeconds;

    public static TimeSpan? EnrageFor(int mobCode, bool isFieldBoss, IReadOnlyDictionary<int, int> overrides) =>
        overrides.TryGetValue(mobCode, out var seconds) ? (seconds > 0 ? TimeSpan.FromSeconds(seconds) : null)
        : isFieldBoss ? null
        : DefaultEnrage;

    public static BossFightView Build(string name, long hpCurrent, long hpTotal, double partyDps, TimeSpan elapsed, TimeSpan? enrage)
    {
        var percent = hpTotal > 0 ? Math.Clamp((double)hpCurrent / hpTotal * 100, 0, 100) : 0;
        TimeSpan? toKill = null;
        if (hpCurrent > 0 && partyDps > 0)
        {
            var seconds = Math.Ceiling(hpCurrent / partyDps);
            if (seconds <= MaxShownSeconds) toKill = TimeSpan.FromSeconds(seconds);
        }
        TimeSpan? toEnrage = enrage is { } e ? (e > elapsed ? e - elapsed : TimeSpan.Zero) : null;
        return new BossFightView(name, hpCurrent, hpTotal, percent, elapsed, toKill, enrage, toEnrage);
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test AionDpsMeter.Timers.Tests`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add AionDpsMeter.Timers AionDpsMeter.Timers.Tests
git commit -m "feat(timers): boss fight estimates — time to kill and enrage" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 2: Лента: расписание отдельно, безымянные боссы одной строкой, стиль главного окна

**Files:**
- Modify: `AionDpsMeter.Timers/Feed/TimersFeed.cs`, `AionDpsMeter.Timers/Feed/TimersOptions.cs`
- Test: `AionDpsMeter.Timers.Tests/Feed/TimersFeedTests.cs`, `AionDpsMeter.Timers.Tests/Feed/TimersOptionsTests.cs`

**Interfaces:**
- Produces: `TimersFeed.BuildSchedule(DateTimeOffset now, ScheduleData data)`, `TimersFeed.BuildBosses(DateTimeOffset now, ScheduleData data, IEnumerable<BossTimer> bosses, BossCatalog catalog)` (оба сортированы); `TimersFeed.Build` = обе, сортированы; групповая строка: `Id = "map:{MapId}"`, `BossKey = null`, `Kind = Boss`. `TimersOptions.MainStyle` (`"boss"` по умолчанию) и `[JsonIgnore] bool UseBossPanel`.

- [ ] **Step 1: Write the failing tests / adjust the old ones**

В `TimersFeedTests`: в `Boss_statuses_follow_the_timer` заменить ключи `-111003/-111004/-111005` на `2400003/2400004/2400005` (имена — «Босс 2400003» и т. д.) и удалить последнюю проверку «Босс №21». В `Items_are_ordered_running_first_then_soonest` заменить `-111001/-111002/-111003` на `2400001/2400002/2400003` (и ожидаемый порядок `[2400002, 2400001, null, 2400003]`). Добавить:

```csharp
    [Fact]
    public void Unnamed_bosses_collapse_into_one_row_per_map()
    {
        var items = TimersFeed.BuildBosses(Now, Data(), [
            new BossTimer(1, -101001, 1010, 101001, true, null, null, null, null),
            new BossTimer(1, -101002, 1010, 101002, true, null, null, null, null),
            new BossTimer(1, -101003, 1010, 101003, false, null, Now.AddMinutes(7), null, null),
            new BossTimer(1, 2400800, 1110, 111021, false, null, Now.AddMinutes(20), null, null),
        ], Catalog);

        Assert.Equal(2, items.Count);
        var group = items.Single(i => i.Id == "map:1010");
        Assert.Equal("Карта 1010: живы 2 из 3", group.Title);
        Assert.Equal(FeedStatus.Upcoming, group.Status);
        Assert.Equal(Now.AddMinutes(7), group.At);
        Assert.Null(group.BossKey);
        Assert.Equal("Альтгард: живы 1 из 1", TimersFeed.BuildBosses(Now, Data(),
            [new BossTimer(1, -111001, 1110, 111001, true, null, null, null, null)], Catalog).Single().Title);
    }

    [Fact]
    public void Schedule_and_bosses_can_be_built_separately()
    {
        Assert.Equal(["rift"], TimersFeed.BuildSchedule(Now, Data(Rift())).Select(i => i.Id));
        Assert.Empty(TimersFeed.BuildBosses(Now, Data(Rift()), [], Catalog));
    }
```

В `TimersOptionsTests` добавить:

```csharp
    [Fact]
    public void The_boss_panel_is_the_default_main_style()
    {
        var options = new TimersOptions();
        Assert.True(options.UseBossPanel);
        options.MainStyle = "upstream";
        Assert.False(options.UseBossPanel);

        using var dir = new TempDir();
        var store = new TimersOptionsStore(dir.File("timers-settings.json"));
        store.Load();
        store.Save();
        Assert.DoesNotContain("UseBossPanel", File.ReadAllText(dir.File("timers-settings.json")));
    }
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test AionDpsMeter.Timers.Tests`
Expected: FAIL — нет `BuildSchedule`/`BuildBosses`/`UseBossPanel`.

- [ ] **Step 3: Write the implementation**

`TimersFeed.cs` — заменить метод `Build` на три:

```csharp
    public static IReadOnlyList<FeedItem> Build(DateTimeOffset now, ScheduleData data, IEnumerable<BossTimer> bosses, BossCatalog catalog) =>
        Sort(BuildSchedule(now, data).Concat(BuildBosses(now, data, bosses, catalog)));

    public static IReadOnlyList<FeedItem> BuildSchedule(DateTimeOffset now, ScheduleData data)
    {
        var items = new List<FeedItem>();
        foreach (var e in data.Events)
        {
            if (ScheduleCalculator.Next(e, now) is not { } o) continue;
            var active = o.IsActive(now);
            var kind = e.Category.Equals("rift", StringComparison.OrdinalIgnoreCase) ? FeedKind.Rift : FeedKind.Event;
            items.Add(new FeedItem(e.Id, $"{e.Id}@{o.Start:O}", kind, e.Name, null,
                active ? FeedStatus.Active : FeedStatus.Upcoming, active ? o.End : o.Start, e.Verified, null));
        }
        return Sort(items);
    }

    /// <summary>Named bosses one row each; bosses of a map not in the tables (unknown names) one summary row per map.</summary>
    public static IReadOnlyList<FeedItem> BuildBosses(DateTimeOffset now, ScheduleData data, IEnumerable<BossTimer> bosses, BossCatalog catalog)
    {
        var items = new List<FeedItem>();
        var list = bosses.ToList();
        foreach (var b in list.Where(b => b.Key > 0))
        {
            var zone = ZoneOf(data, b.MapId);
            var (status, at) = b switch
            {
                { Alive: true } => (FeedStatus.Alive, b.AliveSince),
                { NextSpawn: { } next } when next > now => (FeedStatus.Upcoming, (DateTimeOffset?)next),
                { NextSpawn: { } next } when now - next < OverdueWindow => (FeedStatus.Overdue, (DateTimeOffset?)next),
                _ => (FeedStatus.Unknown, (DateTimeOffset?)null),
            };
            items.Add(new FeedItem($"boss:{b.Key}", $"boss:{b.Key}@{at:O}", FeedKind.Boss, catalog.Name(b.Key), zone, status, at, true, b.Key));
        }
        foreach (var map in list.Where(b => b.Key < 0).GroupBy(b => b.MapId))
        {
            var alive = map.Count(b => b.Alive);
            var upcoming = map.Where(b => !b.Alive && b.NextSpawn > now).Select(b => b.NextSpawn!.Value).ToList();
            DateTimeOffset? next = upcoming.Count > 0 ? upcoming.Min() : null;
            var status = next is not null ? FeedStatus.Upcoming : alive > 0 ? FeedStatus.Alive : FeedStatus.Unknown;
            var title = $"{ZoneOf(data, map.Key) ?? $"Карта {map.Key}"}: живы {alive} из {map.Count()}";
            items.Add(new FeedItem($"map:{map.Key}", $"map:{map.Key}@{next:O}", FeedKind.Boss, title, null, status, next, true, null));
        }
        return Sort(items);
    }

    private static string? ZoneOf(ScheduleData data, int mapId) =>
        data.FieldBossMaps.TryGetValue(mapId, out var map) ? map.Name : null;

    private static IReadOnlyList<FeedItem> Sort(IEnumerable<FeedItem> items) => items
        .OrderBy(i => Rank(i.Status))
        .ThenBy(i => i.At ?? DateTimeOffset.MaxValue)
        .ThenBy(i => i.Title, StringComparer.CurrentCulture)
        .ToList();
```

`TimersOptions` — добавить:

```csharp
    /// <summary>"boss" — the fork's boss panel as the main window; "upstream" — RATmeter's own style.</summary>
    public string MainStyle { get; set; } = "boss";

    [System.Text.Json.Serialization.JsonIgnore]
    public bool UseBossPanel => !string.Equals(MainStyle, "upstream", StringComparison.OrdinalIgnoreCase);
```

- [ ] **Step 4: Run tests to verify they pass** — `dotnet test AionDpsMeter.Timers.Tests` → PASS.

- [ ] **Step 5: Commit** — `git commit -m "feat(timers): split schedule and boss feeds; group unnamed bosses per map" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"`

---

### Task 3: Главное окно «Бой с боссом»

**Files:**
- Create: `AionDpsMeter.UI/Pages/BossPanelPage.razor`, `.razor.cs`, `.razor.css`
- Modify (апстрим): `AionDpsMeter.UI/Pages/MainDpsPage.razor` (+3 строки)

**Interfaces:**
- Consumes: `CombatSessionManager.GetActiveTargetInfo()` → `Mob?` (`Name`, `MobCode`, `HpCurrent`, `HpTotal`, `IsBoss`, `IsDummy`), `GetPartyDps()`, `GetCombatDuration()`; `MainDpsViewModel` (`CombatDuration`, `PingDisplay`, `PingColor`, `HasActiveTarget`, `ActiveTargetName`, `BeginDrag`, `OpenHistory`, `OpenStatEffCalc`, `OpenSettings`, `Minimize`, `Close`); `DamageFormatter.Format(double)`; `MainDpsMinimal` (`ViewModel` parameter); Task 1–2.

- [ ] **Step 1: Страница**

`BossPanelPage.razor`:

```razor
@* aion2-overlay fork: new file (see FORK_CHANGES.md). *@
@using System.Globalization
@using AionDpsMeter.Timers.Feed
@implements IDisposable

@if (ViewModel != null)
{
    <div class="boss-panel">
        <div class="bp-header" @onmousedown="ViewModel.BeginDrag">
            <span class="bp-dot @(Fight is not null ? "bp-dot--fight" : "")"></span>
            <span class="bp-title" title="@HeaderTitle">@HeaderTitle</span>
            <span class="bp-time">@ViewModel.CombatDuration</span>
            <span class="bp-ping" style="color:@ViewModel.PingColor">@ViewModel.PingDisplay</span>
            <div class="bp-controls">
                <button type="button" @onclick="ViewModel.OpenHistory" @onmousedown:stopPropagation="true" title="История боёв">☰</button>
                <button type="button" @onclick="ViewModel.OpenStatEffCalc" @onmousedown:stopPropagation="true" title="Калькулятор статов">⚔</button>
                <button type="button" @onclick="ViewModel.OpenSettings" @onmousedown:stopPropagation="true" title="Настройки">⚙</button>
                <button type="button" @onclick="ViewModel.Minimize" @onmousedown:stopPropagation="true" title="Свернуть">_</button>
                <button type="button" @onclick="ViewModel.Close" @onmousedown:stopPropagation="true" title="Закрыть">✕</button>
            </div>
        </div>
        @if (Fight is { } f)
        {
            <div class="bp-card">
                <div class="bp-card-top">
                    <span class="bp-skull">☠</span>
                    <span class="bp-name" title="@f.Name">@f.Name</span>
                    <span class="bp-hp">@Hp(f.HpCurrent) / @Hp(f.HpTotal) · <b>@f.HpPercent.ToString("0", CultureInfo.InvariantCulture)%</b></span>
                </div>
                <div class="bp-track"><div class="bp-fill" style="width:@(f.HpPercent.ToString("0.##", CultureInfo.InvariantCulture))%"></div></div>
                <div class="bp-card-bottom">
                    <span>До победы <b class="bp-win">@(f.ToKill is { } k ? FeedText.Span(k) : "—")</b></span>
                    @if (f.Enrage is not null)
                    {
                        <span>До ярости <b class="@(f.Enraged ? "bp-rage" : "bp-enrage")">@(f.Enraged ? "ярость!" : FeedText.Span(f.ToEnrage!.Value))</b></span>
                    }
                </div>
            </div>
        }
        <div class="bp-players">
            <MainDpsMinimal ViewModel="@ViewModel" />
        </div>
        @if (NextEvent is { } e)
        {
            <div class="bp-footer">
                <span>◈ @e.Title@(e.Verified ? "" : " (не проверено)")</span>
                <span class="bp-footer-time">@FeedText.Format(e, now, TimeZoneInfo.Local)</span>
            </div>
        }
    </div>
}
```

`BossPanelPage.razor.cs`:

```csharp
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
```

`BossPanelPage.razor.css`:

```css
/* aion2-overlay fork: new file (see FORK_CHANGES.md). */
.boss-panel { display: flex; flex-direction: column; height: 100%; box-sizing: border-box; color: #e6edf3; user-select: none;
    background: rgba(18, 16, 24, .82); border-radius: 10px; overflow: hidden; font-size: 13px; }
.bp-header { display: flex; align-items: center; gap: 8px; padding: 6px 10px; cursor: move; }
.bp-dot { width: 8px; height: 8px; border-radius: 50%; background: #7ee787; flex: 0 0 auto; }
.bp-dot--fight { background: #f2cc60; box-shadow: 0 0 6px #f2cc60; }
.bp-title { font-weight: 700; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
.bp-time { color: #7ee787; font-variant-numeric: tabular-nums; }
.bp-ping { font-size: 11px; margin-left: auto; }
.bp-controls { display: flex; gap: 2px; }
.bp-controls button { background: none; border: 0; color: #9aa4b2; cursor: pointer; padding: 2px 5px; font-size: 13px; }
.bp-controls button:hover { color: #fff; }
.bp-card { margin: 0 8px 6px; padding: 8px 10px; border-radius: 8px; background: rgba(255, 255, 255, .06);
    border: 1px solid rgba(255, 255, 255, .08); }
.bp-card-top { display: flex; align-items: center; gap: 6px; }
.bp-skull { opacity: .8; }
.bp-name { font-weight: 700; flex: 1; min-width: 0; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
.bp-hp { color: #9aa4b2; font-variant-numeric: tabular-nums; white-space: nowrap; }
.bp-hp b { color: #ff8fa3; }
.bp-track { height: 6px; margin: 6px 0; border-radius: 3px; background: rgba(255, 255, 255, .1); overflow: hidden; }
.bp-fill { height: 100%; background: linear-gradient(90deg, #ff6b81, #ff9aa8); transition: width .2s; }
.bp-card-bottom { display: flex; justify-content: space-between; color: #9aa4b2; font-size: 12px; }
.bp-card-bottom b { font-variant-numeric: tabular-nums; }
.bp-win { color: #7ee787; }
.bp-enrage { color: #f2cc60; }
.bp-rage { color: #ff6b6b; }
.bp-players { flex: 1; min-height: 0; overflow: auto; }
.bp-players ::deep .header-module { display: none; } /* upstream's own header: ours replaces it */
.bp-footer { display: flex; justify-content: space-between; gap: 8px; padding: 5px 10px; font-size: 12px;
    border-top: 1px solid rgba(255, 255, 255, .08); color: #c9d1d9; }
.bp-footer-time { font-variant-numeric: tabular-nums; color: #f2cc60; white-space: nowrap; }
```

- [ ] **Step 2: Выбор страницы в `MainDpsPage.razor` (3 строки, строки апстрима не меняются)**

Сразу после строки `@page "/"` добавить:

```razor
@inject AionDpsMeter.Timers.Feed.TimersOptionsStore TimersOptions @* aion2-overlay fork *@
@if (TimersOptions.Current.UseBossPanel) { <BossPanelPage ViewModel="@viewModel" /> } else { @* aion2-overlay fork *@
```

и сразу после закрывающей `}` блока `@if(UiStyle == 1) … else …` (перед `@code`) добавить строку:

```razor
} @* aion2-overlay fork *@
```

- [ ] **Step 3: Собрать и проверить**

Run: `dotnet build AionDpsMeter.slnx -c Release && dotnet test AionDpsMeter.Timers.Tests`
Expected: успех. Запустить приложение (как в Task 9 первого плана), снять область главного окна: шапка «Ожидание боя»/имя цели, строки игроков без второй шапки, внизу «◈ Разлом … через …». Карточку босса проверяет пользователь в бою.

- [ ] **Step 4: Commit** — `git commit -m "feat(ui): boss fight main window with time to kill, enrage and next rift" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"`

---

### Task 4: Окно таймеров — только боссы, размер, подсказка, без «сервер не определён»

**Files:**
- Modify: `AionDpsMeter.UI/Pages/TimersOverlay.razor`, `.razor.cs`, `.razor.css`, `AionDpsMeter.UI/Services/TimerWindow/TimersWindowController.cs`, `AionDpsMeter.Timers/Runtime/EntityTrackerAdapters.cs`
- Test: `AionDpsMeter.Timers.Tests/Runtime/EntityTrackerAdaptersTests.cs`

**Interfaces:**
- `EntityTrackerServerContext(EntityTracker entities, ILogger<EntityTrackerServerContext> logger)` — пишет в лог один раз, если у персонажа нет имени сервера.

- [ ] **Step 1: Failing test** — в `EntityTrackerAdaptersTests` передавать `NullLogger<EntityTrackerServerContext>.Instance` во все конструкторы и добавить:

```csharp
    [Fact]
    public void A_server_missing_from_upstreams_table_stays_zero_even_in_a_party()
    {
        // EU server ids are not in RATmeter's ServerMap: the name is empty. Party data would give a raw id, but using it
        // would split the timers every time the player joins a party.
        var entities = new EntityTracker();
        entities.SetSessionPlayerName(10, "Me", 0, "", isUser: true);
        entities.GetPlayerEntity(10)!.ServerId = 3001;

        Assert.Equal(0, new EntityTrackerServerContext(entities, NullLogger<EntityTrackerServerContext>.Instance).CurrentServerId);
    }
```

Run → FAIL (компиляция: конструктор с логгером).

- [ ] **Step 2: Implementation** — в `EntityTrackerServerContext` добавить параметр `ILogger<EntityTrackerServerContext> logger`, поле `private bool reported;` и после выбора `user`:

```csharp
                if (user is null && !reported
                    && entities.PlayerEntities.FirstOrDefault(p => p.IsUser) is { } unnamed)
                {
                    reported = true;
                    logger.LogInformation(
                        "Played character's server is not in RATmeter's server table (party server id {ServerId}); boss timers use one shared group",
                        unnamed.ServerId);
                }
```

(используется `using Microsoft.Extensions.Logging;`). `dotnet test` → PASS.

- [ ] **Step 3: Окно**

`TimersOverlay.razor.cs`:
- в `Recompute` строить два списка: `var all = TimersFeed.Build(...)` для оповещений (как раньше) и `visible = TimersFeed.BuildBosses(now, schedule.Current, tracker.TimersFor(server.CurrentServerId), catalog).Where(Options.Shows).ToList();`. Оповещения считать по `all.Where(Options.Shows)`.
- удалить свойство `ServerUnknown`.

`TimersOverlay.razor`:
- удалить блок `@if (ServerUnknown) { … }`;
- заменить пустое состояние на `@if (visible.Count == 0 && IsEditing) { <div class="timers__empty">Боссов пока нет. Открой карту в игре — появятся полевые боссы.</div> }`;
- в конец корневого `div` добавить `<div class="timers__hint">@EditHotkey — переместить / размер</div>`.

`TimersOverlay.razor.css` — добавить:

```css
.timers { display: flex; flex-direction: column; height: 100%; }
.timers__list { flex: 1; min-height: 0; overflow: auto; }
.timers__hint { opacity: .35; font-size: 10px; padding: 2px 6px; text-align: right; }
```

(строки `@foreach (var item in visible)` обернуть в `<div class="timers__list">…</div>`).

`TimersWindowController.Open()` — у окна задать `ResizeMode = ResizeMode.CanResizeWithGrip, MinWidth = 220, MinHeight = 80` и открывать с `persistenceMode: WindowPersistenceMode.Bounds` (позиция и размер).

- [ ] **Step 4: Проверить** — сборка, тесты; запуск: в окне таймеров нет «Сервер не определён», нет строки разлома, внизу подсказка; в логе строка про сервер (если персонаж в игре).

- [ ] **Step 5: Commit** — `git commit -m "feat(ui): timers window shows bosses only, resizes, hints its hotkey" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"`

---

### Task 5: Русский перевод окон RATmeter

**Files:**
- Create: `AionDpsMeter.UI/wwwroot/js/ru.js`
- Modify (апстрим): `AionDpsMeter.UI/wwwroot/index.html` (+1 строка)

- [ ] **Step 1: Словарь и применение**

`AionDpsMeter.UI/wwwroot/js/ru.js`:

```js
// aion2-overlay fork: new file (see FORK_CHANGES.md).
// Russian for upstream's Blazor windows. Texts and title/placeholder attributes found in the dictionary are replaced
// whenever the page changes; anything not in the dictionary stays English (a new upstream string never breaks).
(function () {
    const ru = {
        "Settings": "Настройки", "Close": "Закрыть", "Close settings": "Закрыть настройки",
        "Appearance": "Внешний вид", "Hotkeys": "Горячие клавиши", "Tracking": "Учёт урона", "Developer": "Разработчику",
        "Window layout": "Расположение окон", "WINDOW LAYOUT": "РАСПОЛОЖЕНИЕ ОКОН", "WINDOW": "ОКНО",
        "PLAYER LIST": "СПИСОК ИГРОКОВ", "HISTORY": "ИСТОРИЯ", "BUFF OVERLAY": "ОВЕРЛЕЙ БАФФОВ",
        "SKILL COOLDOWNS": "ПЕРЕЗАРЯДКА УМЕНИЙ", "Settings groups": "Разделы настроек",
        "Boss Encounters Only": "Только бои с боссами",
        "Ignore damage dealt to trash mobs and only track boss fights": "Не считать урон по обычным мобам, только бои с боссами",
        "Cap Dummy Parses at 1 Minute": "Манекен — не дольше 1 минуты",
        "Stop counting damage on training dummies after 60 seconds": "Прекращать подсчёт урона по манекену через 60 секунд",
        "Combine Summon Damage": "Объединять урон призывов",
        "Merge all pet and summon damage into the owner's row": "Урон питомцев и призывов добавлять в строку владельца",
        "Hide Player Names": "Скрыть имена игроков",
        "Replace names with obfuscated names — useful before sharing screenshots": "Заменять имена — удобно перед тем, как делиться скриншотами",
        "Show Death Count": "Показывать смерти",
        "Display a skull icon with death count on each row": "Иконка черепа с числом смертей в каждой строке",
        "Relative Damage Bars": "Полосы относительно лидера",
        "Scale bars against the top player instead of absolute damage": "Длина полос — относительно лучшего игрока, а не абсолютного урона",
        "Use class colors": "Цвета классов",
        "Use class colors for player rows instead of highlighting me": "Красить строки в цвета классов вместо выделения себя",
        "Row Size": "Размер строки", "Size of each player row": "Высота строки игрока",
        "Window Opacity": "Прозрачность окна", "How see-through the overlay is (10–100%)": "Насколько прозрачен оверлей (10–100%)",
        "Toggle Visibility": "Показать / скрыть",
        "Click the field, then press the key combo you want to use": "Нажми на поле, затем нужное сочетание клавиш",
        "Press a key...": "Нажми клавишу...",
        "Keep History For": "Хранить историю",
        "Older encounters are automatically deleted after this many days": "Старые бои удаляются автоматически через столько дней",
        "days": "дн.",
        "Log Raw Packets": "Записывать сырые пакеты",
        "Save incoming network packets to the PacketLogs folder": "Сохранять входящие пакеты в папку PacketLogs",
        "Enabled": "Включено",
        "Show a floating overlay with active buff timers": "Плавающее окно с таймерами активных баффов",
        "Show a floating overlay with active skill cd timers": "Плавающее окно с перезарядкой умений",
        "Icon Size": "Размер иконок", "Size of each buff icon (20–50px)": "Размер иконки баффа (20–50 px)",
        "Size of each skill icon (20–50px)": "Размер иконки умения (20–50 px)",
        "Sort Order": "Порядок", "How tracked buffs are ordered by remaining time": "Сортировка баффов по оставшемуся времени",
        "How tracked skills are ordered by remaining cd time": "Сортировка умений по оставшейся перезарядке",
        "Least time": "Сначала меньше", "Most time": "Сначала больше",
        "Tracked Buffs": "Отслеживаемые баффы", "Tracked skills": "Отслеживаемые умения",
        "Search skills…": "Поиск умений…", "No skills found": "Умения не найдены", "Add skill": "Добавить умение",
        "Standard": "Обычный", "Compact": "Компактный",
        "History": "История", "Stat Eff Calculator": "Калькулятор статов", "Hide": "Свернуть",
        "What's New": "Что нового"
    };

    const attributes = ["title", "placeholder"];

    function translate(text) {
        if (!text) return null;
        const key = text.trim();
        const value = Object.prototype.hasOwnProperty.call(ru, key) ? ru[key] : undefined;
        return value === undefined ? null : text.replace(key, value);
    }

    function fixText(node) {
        const t = translate(node.nodeValue);
        if (t !== null && t !== node.nodeValue) node.nodeValue = t;
    }

    function fixAttribute(element, name) {
        const v = element.getAttribute(name);
        const t = translate(v);
        if (t !== null && t !== v) element.setAttribute(name, t);
    }

    function walk(root) {
        const walker = document.createTreeWalker(root, NodeFilter.SHOW_TEXT);
        for (let n = walker.nextNode(); n; n = walker.nextNode()) fixText(n);
        const elements = [root, ...root.querySelectorAll("[title],[placeholder]")];
        for (const el of elements) if (el.getAttribute) for (const a of attributes) if (el.hasAttribute(a)) fixAttribute(el, a);
    }

    function start() {
        walk(document.body);
        new MutationObserver(function (mutations) {
            for (const m of mutations) {
                if (m.type === "characterData") fixText(m.target);
                else if (m.type === "attributes") fixAttribute(m.target, m.attributeName);
                else for (const n of m.addedNodes) {
                    if (n.nodeType === Node.TEXT_NODE) fixText(n);
                    else if (n.nodeType === Node.ELEMENT_NODE) walk(n);
                }
            }
        }).observe(document.body, { childList: true, subtree: true, characterData: true, attributes: true, attributeFilter: attributes });
    }

    if (typeof module !== "undefined") module.exports = { translate };
    else if (document.readyState === "loading") document.addEventListener("DOMContentLoaded", start);
    else start();
})();
```

- [ ] **Step 2: Проверить логику перевода в node (без браузера)**

```bash
node -e "const {translate}=require('./AionDpsMeter.UI/wwwroot/js/ru.js'); const a=translate('  Settings '); const ok=a==='  Настройки ' && translate('Настройки')===null && translate('Unknown')===null && translate('')===null; console.log(ok?'PASS':'FAIL', JSON.stringify(a)); process.exit(ok?0:1)"
```
Expected: `PASS "  Настройки "` (русский текст не переводится повторно → нет зацикливания).

- [ ] **Step 3: Подключить** — в `AionDpsMeter.UI/wwwroot/index.html` перед строкой `<script src="_framework/blazor.webview.js"></script>` добавить:

```html
    <script src="js/ru.js"></script> <!-- aion2-overlay fork -->
```

- [ ] **Step 4: Проверить вживую** — сборка; запуск; пользователь открывает ⚙ — надписи на русском.

- [ ] **Step 5: Commit** — `git commit -m "feat(ui): Russian for upstream's Blazor windows via a runtime dictionary" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"`

---

### Task 6: Документация, данные, итог

**Files:** `schedule.json`, `FORK_CHANGES.md`, `docs/USAGE.ru.md`

- [ ] **Step 1:** `schedule.json` — добавить `"enrageSeconds": {}` и в `_note` фразу «enrageSeconds: код босса → секунды до ярости (0 — нет ярости); по умолчанию 300 у боссов данжей».
- [ ] **Step 2:** `FORK_CHANGES.md` — в таблицу: `AionDpsMeter.UI/Pages/MainDpsPage.razor` (+3 строки: выбор `BossPanelPage`), `AionDpsMeter.UI/wwwroot/index.html` (+1 строка: `js/ru.js`); в новые файлы: `BossPanelPage.*`, `wwwroot/js/ru.js`.
- [ ] **Step 3:** `docs/USAGE.ru.md` — раздел «Главное окно»: что показывает карточка боя, откуда «до ярости», как вернуть стиль RATmeter (`"MainStyle": "upstream"` в `timers-settings.json`); окно таймеров — «только полевые боссы; размер — за правый нижний угол в режиме настройки».
- [ ] **Step 4:** `dotnet build AionDpsMeter.slnx -c Release && dotnet test AionDpsMeter.Timers.Tests` → PASS; commit `docs: boss panel, Russian UI, enrage overrides`.
