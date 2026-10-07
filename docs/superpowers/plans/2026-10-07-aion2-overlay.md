# AION 2 оверлей: таймеры поверх RATmeter — план реализации

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Форк RATmeter с модулем таймеров (полевые боссы, разломы, ивенты), окном-оверлеем таймеров и ежедневным автообновлением из апстрима.

**Architecture:** Рабочая копия `Desktop/AION2` = форк `drixed/aion2-overlay` от `Kuroukihime/AIon2-Dps-Meter` (ветка `master`). Вся логика таймеров — в новом проекте `AionDpsMeter.Timers` (без UI, покрыт xUnit-тестами). Пакеты попадают в него через новый файл-переходник внутри `AionDpsMeter.Services`; окно — новый Blazor-компонент в `AionDpsMeter.UI`. Правки файлов апстрима — 4 места по 1–2 строки, все перечислены в `FORK_CHANGES.md`. GitHub Actions раз в сутки сливает апстрим, гоняет тесты и выпускает релиз; встроенный обновлятор RATmeter смотрит на релизы форка.

**Tech Stack:** C# / .NET 10, WPF + Blazor WebView (как в апстриме), xUnit, Microsoft.Extensions.TimeProvider.Testing, GitHub Actions (windows-latest, pwsh).

**Spec:** `docs/superpowers/specs/2026-10-07-aion2-overlay-design.md`

## Global Constraints

- Целевая платформа: `net10.0` для `AionDpsMeter.Timers` и тестов (как `AionDpsMeter.Services`); UI остаётся `net10.0-windows…`.
- Апстрим: `https://github.com/Kuroukihime/AIon2-Dps-Meter.git`, ветка `master`. Форк: `https://github.com/drixed/aion2-overlay`, ветка `master`.
- Файлы апстрима правим только в местах, перечисленных в `FORK_CHANGES.md` (Task 10). Новые файлы в проектах апстрима помечаем первой строкой `// aion2-overlay fork: new file (see FORK_CHANGES.md).`
- Версия `AssemblyVersion` в `.csproj` не трогается; CI задаёт `ГГГГ.ММДД.N` через `-p:Version/-p:AssemblyVersion/-p:FileVersion`.
- Удалённое расписание: `https://raw.githubusercontent.com/drixed/aion2-overlay/master/schedule.json`.
- Файлы состояния — рядом с exe (`AppContext.BaseDirectory`): `timers-state.json`, `timers-settings.json`, `schedule-cache.json`.
- Тексты интерфейса — на русском. Лицензия — GPL-3.0 (как апстрим).
- Исключения кода таймеров никогда не должны ронять конвейер пакетов апстрима или UI.
- `dotnet` после установки может отсутствовать в PATH текущей оболочки: в bash выполнять `export PATH="$PATH:/c/Program Files/dotnet"`.

## Review Focus

1. **Протокол поменялся после патча** — пакет списка боссов обрезан/мусор → парсер возвращает `null` или неполный список, никогда не бросает (тест в Task 4).
2. **Апстрим занял опкод `0x9101`** — реестр RATmeter бросает исключение на старте → приложение не запускается. Тест в Task 7 проверяет, что опкод `0x9101` объявлен ровно одним классом; CI его гоняет перед каждым релизом.
3. **Нет сети / битый `schedule.json` на GitHub** — остаются кэш или встроенная копия, кэш не перезаписывается мусором (тесты в Task 3).
4. **Персонаж на другом сервере** — таймеры одного сервера не показываются на другом (тест в Task 6).
5. **Часовые пояса и переход на летнее время** — расписание в `Europe/Berlin`, у пользователя может быть другой пояс; время, «съеденное» переходом на летнее время, не ломает расчёт (тесты в Task 2).

---

### Task 1: Окружение, форк и рабочая копия

**Files:**
- Modify: рабочая папка `C:\Users\drixxxed\Desktop\AION2` становится git-репозиторием форка (папка `docs/` уже есть и сохраняется).

**Interfaces:**
- Consumes: —
- Produces: собирающийся `AionDpsMeter.slnx` на ветке `master` с удалёнными `origin` (форк) и `upstream`.

- [ ] **Step 1: Спросить пользователя и установить .NET 10 SDK**

Установка принимает лицензионное соглашение winget — перед запуском получить явное «да» пользователя.

```bash
winget install --id Microsoft.DotNet.SDK.10 -e --silent --accept-package-agreements --accept-source-agreements
export PATH="$PATH:/c/Program Files/dotnet"
dotnet --list-sdks
```
Expected: строка `10.0.xxx [C:\Program Files\dotnet\sdk]`.

- [ ] **Step 2: Создать форк на GitHub**

```bash
gh repo fork Kuroukihime/AIon2-Dps-Meter --fork-name aion2-overlay --clone=false
gh repo view drixed/aion2-overlay --json name,defaultBranchRef -q '.name + " " + .defaultBranchRef.name'
```
Expected: `aion2-overlay master`.

- [ ] **Step 3: Превратить папку в рабочую копию форка**

```bash
cd /c/Users/drixxxed/Desktop/AION2
git init -b master
git remote add origin https://github.com/drixed/aion2-overlay.git
git remote add upstream https://github.com/Kuroukihime/AIon2-Dps-Meter.git
git fetch origin
git fetch upstream
git checkout -B master --track origin/master
git status --short
```
Expected: в `git status` только `?? docs/`.

- [ ] **Step 4: Проверить, что апстрим собирается как есть**

```bash
dotnet build AionDpsMeter.slnx -c Release
```
Expected: `Build succeeded` (предупреждения допустимы). Если сборка падает — остановиться и разобраться до любых изменений.

- [ ] **Step 5: Commit**

```bash
git add docs
git commit -m "docs: overlay design and implementation plan"
```

---

### Task 2: Проект таймеров и расчёт расписания

**Files:**
- Create: `AionDpsMeter.Timers/AionDpsMeter.Timers.csproj`
- Create: `AionDpsMeter.Timers/Schedule/ScheduledEvent.cs`
- Create: `AionDpsMeter.Timers/Schedule/ScheduleCalculator.cs`
- Create: `AionDpsMeter.Timers.Tests/AionDpsMeter.Timers.Tests.csproj`
- Test: `AionDpsMeter.Timers.Tests/Schedule/ScheduleCalculatorTests.cs`
- Modify (апстрим): `AionDpsMeter.slnx` (+2 проекта), `AionDpsMeter.UI/AionDpsMeter.UI.csproj` (+1 `ProjectReference`)

**Interfaces:**
- Consumes: —
- Produces:
  - `enum ScheduleKind { Fixed, Interval }`
  - `record ScheduledEvent(string Id, string Name, string Category, ScheduleKind Kind, TimeZoneInfo TimeZone, IReadOnlyList<TimeOnly> Times, IReadOnlySet<DayOfWeek>? Days, DateTimeOffset? Anchor, TimeSpan? Period, TimeSpan Duration, bool Verified)`
  - `readonly record struct Occurrence(DateTimeOffset Start, DateTimeOffset End)` с `bool IsActive(DateTimeOffset now)`
  - `static Occurrence? ScheduleCalculator.Next(ScheduledEvent e, DateTimeOffset now)` — идущее сейчас вхождение, иначе ближайшее будущее.

- [ ] **Step 1: Создать проекты и связи**

```bash
cd /c/Users/drixxxed/Desktop/AION2
dotnet new classlib -n AionDpsMeter.Timers -f net10.0 -o AionDpsMeter.Timers
rm AionDpsMeter.Timers/Class1.cs
dotnet new xunit -n AionDpsMeter.Timers.Tests -f net10.0 -o AionDpsMeter.Timers.Tests
rm -f AionDpsMeter.Timers.Tests/UnitTest1.cs
dotnet sln AionDpsMeter.slnx add AionDpsMeter.Timers/AionDpsMeter.Timers.csproj AionDpsMeter.Timers.Tests/AionDpsMeter.Timers.Tests.csproj
dotnet add AionDpsMeter.Timers reference AionDpsMeter.Services/AionDpsMeter.Services.csproj
dotnet add AionDpsMeter.Timers.Tests reference AionDpsMeter.Timers/AionDpsMeter.Timers.csproj
dotnet add AionDpsMeter.Timers.Tests package Microsoft.Extensions.TimeProvider.Testing
dotnet add AionDpsMeter.UI reference AionDpsMeter.Timers/AionDpsMeter.Timers.csproj
```

Затем в `AionDpsMeter.Timers/AionDpsMeter.Timers.csproj` внутрь `<Project>` добавить:

```xml
  <ItemGroup>
    <InternalsVisibleTo Include="AionDpsMeter.Timers.Tests" />
  </ItemGroup>
```

- [ ] **Step 2: Write the failing tests**

`AionDpsMeter.Timers.Tests/Schedule/ScheduleCalculatorTests.cs`:

```csharp
using System.Globalization;
using AionDpsMeter.Timers.Schedule;

namespace AionDpsMeter.Timers.Tests.Schedule;

public class ScheduleCalculatorTests
{
    private static readonly TimeZoneInfo Berlin = TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");
    private static readonly string[] EveryThreeHours = ["02:00", "05:00", "08:00", "11:00", "14:00", "17:00", "20:00", "23:00"];

    private static ScheduledEvent Fixed(string[] times, int durationMinutes = 0, DayOfWeek[]? days = null) =>
        new("e", "E", "event", ScheduleKind.Fixed, Berlin,
            times.Select(t => TimeOnly.Parse(t, CultureInfo.InvariantCulture)).ToList(),
            days?.ToHashSet(), null, null, TimeSpan.FromMinutes(durationMinutes), true);

    private static ScheduledEvent Interval(DateTimeOffset anchor, int periodMinutes, int durationMinutes = 0) =>
        new("i", "I", "event", ScheduleKind.Interval, TimeZoneInfo.Utc, [], null,
            anchor, TimeSpan.FromMinutes(periodMinutes), TimeSpan.FromMinutes(durationMinutes), true);

    private static DateTimeOffset InBerlin(int y, int mo, int d, int h, int mi)
    {
        var local = new DateTime(y, mo, d, h, mi, 0);
        return new DateTimeOffset(local, Berlin.GetUtcOffset(local));
    }

    private static DateTimeOffset Utc(int d, int h, int mi) => new(2026, 10, d, h, mi, 0, TimeSpan.Zero);

    [Fact]
    public void Fixed_returns_next_time_today()
    {
        var o = ScheduleCalculator.Next(Fixed(EveryThreeHours), InBerlin(2026, 10, 7, 10, 30))!.Value;
        Assert.Equal(InBerlin(2026, 10, 7, 11, 0), o.Start);
        Assert.Equal(o.Start, o.End);
    }

    [Fact]
    public void Fixed_returns_the_occurrence_running_now()
    {
        var now = InBerlin(2026, 10, 7, 11, 20);
        var o = ScheduleCalculator.Next(Fixed(EveryThreeHours, durationMinutes: 60), now)!.Value;
        Assert.Equal(InBerlin(2026, 10, 7, 11, 0), o.Start);
        Assert.True(o.IsActive(now));
    }

    [Fact]
    public void Fixed_wraps_past_midnight()
    {
        var o = ScheduleCalculator.Next(Fixed(["23:00"]), InBerlin(2026, 10, 7, 23, 30))!.Value;
        Assert.Equal(InBerlin(2026, 10, 8, 23, 0), o.Start);
    }

    [Fact]
    public void Fixed_respects_days_of_week()
    {
        // 2026-10-07 is a Wednesday; the next Saturday is 2026-10-10.
        var o = ScheduleCalculator.Next(Fixed(["20:00"], days: [DayOfWeek.Saturday]), InBerlin(2026, 10, 7, 21, 0))!.Value;
        Assert.Equal(InBerlin(2026, 10, 10, 20, 0), o.Start);
    }

    [Fact]
    public void Fixed_survives_the_spring_dst_gap()
    {
        // 2026-03-29 02:00 → 03:00 in Berlin: 02:30 does not exist and moves to 03:30 CEST (01:30 UTC).
        var o = ScheduleCalculator.Next(Fixed(["02:30"]), InBerlin(2026, 3, 29, 0, 0))!.Value;
        Assert.Equal(new DateTimeOffset(2026, 3, 29, 1, 30, 0, TimeSpan.Zero), o.Start);
    }

    [Fact]
    public void The_users_own_offset_does_not_change_the_answer()
    {
        var berlin = InBerlin(2026, 10, 7, 10, 30);
        var tokyo = berlin.ToOffset(TimeSpan.FromHours(9));
        Assert.Equal(ScheduleCalculator.Next(Fixed(EveryThreeHours), berlin)!.Value.Start,
                     ScheduleCalculator.Next(Fixed(EveryThreeHours), tokyo)!.Value.Start);
    }

    [Fact]
    public void Interval_steps_from_the_anchor()
    {
        var e = Interval(Utc(7, 0, 0), periodMinutes: 90);
        Assert.Equal(Utc(7, 1, 30), ScheduleCalculator.Next(e, Utc(7, 1, 0))!.Value.Start);
        Assert.Equal(Utc(7, 3, 0), ScheduleCalculator.Next(e, Utc(7, 1, 30))!.Value.Start);
        Assert.Equal(Utc(7, 0, 0), ScheduleCalculator.Next(e, Utc(6, 22, 0))!.Value.Start);
    }

    [Fact]
    public void Interval_returns_the_occurrence_running_now()
    {
        var now = Utc(7, 1, 40);
        var o = ScheduleCalculator.Next(Interval(Utc(7, 0, 0), 90, durationMinutes: 30), now)!.Value;
        Assert.Equal(Utc(7, 1, 30), o.Start);
        Assert.True(o.IsActive(now));
    }
}
```

- [ ] **Step 3: Run tests to verify they fail**

Run: `dotnet test AionDpsMeter.Timers.Tests`
Expected: FAIL — ошибка компиляции «The type or namespace name 'Schedule' does not exist».

- [ ] **Step 4: Write the implementation**

`AionDpsMeter.Timers/Schedule/ScheduledEvent.cs`:

```csharp
namespace AionDpsMeter.Timers.Schedule;

public enum ScheduleKind { Fixed, Interval }

/// <summary>One event from schedule.json. Fixed: <see cref="Times"/> of day (optionally on <see cref="Days"/>) in
/// <see cref="TimeZone"/>. Interval: <see cref="Anchor"/> + k × <see cref="Period"/>.</summary>
public sealed record ScheduledEvent(
    string Id,
    string Name,
    string Category,
    ScheduleKind Kind,
    TimeZoneInfo TimeZone,
    IReadOnlyList<TimeOnly> Times,
    IReadOnlySet<DayOfWeek>? Days,
    DateTimeOffset? Anchor,
    TimeSpan? Period,
    TimeSpan Duration,
    bool Verified);

public readonly record struct Occurrence(DateTimeOffset Start, DateTimeOffset End)
{
    public bool IsActive(DateTimeOffset now) => Start <= now && now < End;
}
```

`AionDpsMeter.Timers/Schedule/ScheduleCalculator.cs`:

```csharp
namespace AionDpsMeter.Timers.Schedule;

public static class ScheduleCalculator
{
    /// <summary>The occurrence running now, else the next one; null when the event has no valid timing.</summary>
    public static Occurrence? Next(ScheduledEvent e, DateTimeOffset now) => e.Kind switch
    {
        ScheduleKind.Fixed => NextFixed(e, now),
        ScheduleKind.Interval => NextInterval(e, now),
        _ => null,
    };

    private static Occurrence? NextFixed(ScheduledEvent e, DateTimeOffset now)
    {
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, e.TimeZone).DateTime);
        Occurrence? best = null;
        for (var d = -1; d <= 7; d++)
        {
            var date = today.AddDays(d);
            if (e.Days is { } days && !days.Contains(date.DayOfWeek)) continue;
            foreach (var time in e.Times)
            {
                var local = date.ToDateTime(time);
                if (e.TimeZone.IsInvalidTime(local)) local = local.AddHours(1); // skipped by the spring DST jump
                var start = new DateTimeOffset(local, e.TimeZone.GetUtcOffset(local));
                var end = start + e.Duration;
                if (end <= now) continue; // over (with no duration: started already)
                if (best is null || start < best.Value.Start) best = new Occurrence(start, end);
            }
        }
        return best;
    }

    private static Occurrence? NextInterval(ScheduledEvent e, DateTimeOffset now)
    {
        if (e.Anchor is not { } anchor || e.Period is not { } period || period <= TimeSpan.Zero) return null;
        // The first k with anchor + k·period + duration > now.
        var since = now - anchor - e.Duration;
        var k = since < TimeSpan.Zero ? 0 : (long)Math.Floor(since / period) + 1;
        var start = anchor + TimeSpan.FromTicks(period.Ticks * k);
        return new Occurrence(start, start + e.Duration);
    }
}
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test AionDpsMeter.Timers.Tests`
Expected: PASS (8 tests).

- [ ] **Step 6: Commit**

```bash
git add AionDpsMeter.Timers AionDpsMeter.Timers.Tests AionDpsMeter.slnx AionDpsMeter.UI/AionDpsMeter.UI.csproj
git commit -m "feat(timers): timers project and schedule calculator"
```

---

### Task 3: Файл расписания и его загрузка (GitHub → кэш → встроенный)

**Files:**
- Create: `schedule.json` (корень репозитория)
- Create: `AionDpsMeter.Timers/EmbeddedResources.cs`
- Create: `AionDpsMeter.Timers/Schedule/ScheduleData.cs`
- Create: `AionDpsMeter.Timers/Schedule/ScheduleJson.cs`
- Create: `AionDpsMeter.Timers/Schedule/ScheduleSource.cs`
- Modify: `AionDpsMeter.Timers/AionDpsMeter.Timers.csproj` (встроить `schedule.json`)
- Test: `AionDpsMeter.Timers.Tests/Schedule/ScheduleJsonTests.cs`, `AionDpsMeter.Timers.Tests/Schedule/ScheduleSourceTests.cs`, `AionDpsMeter.Timers.Tests/TempDir.cs`

**Interfaces:**
- Consumes: `ScheduledEvent`, `ScheduleKind` (Task 2).
- Produces:
  - `record FieldBossMapInfo(int Block, string Name)`
  - `record ScheduleData(IReadOnlyList<ScheduledEvent> Events, IReadOnlyDictionary<int, FieldBossMapInfo> FieldBossMaps, IReadOnlyDictionary<int, int> RespawnMinutes)` + `static ScheduleData Empty`
  - `static ScheduleData ScheduleJson.Parse(string json)` — бросает `FormatException` на битом JSON; битые события пропускает.
  - `class ScheduleSource(HttpClient http, Uri remote, string cachePath, Func<string> embedded)`: `ScheduleData Current`, `string Origin`, `event Action? Changed`, `void LoadLocal()`, `Task<bool> RefreshAsync(CancellationToken ct = default)`, `const string DefaultRemote`, `static string ReadEmbedded()`
  - `internal static string EmbeddedResources.Read(string name)`

- [ ] **Step 1: Создать `schedule.json` и встроить его в сборку**

`schedule.json`:

```json
{
  "version": 1,
  "_note": "Время — в часовом поясе timeZone. verified=false: не сверено на EU, оверлей пометит событие «(не проверено)». После правки этого файла на GitHub программа подхватит его при следующем запуске.",
  "events": [
    {
      "id": "rift",
      "name": "Разлом",
      "category": "rift",
      "kind": "fixed",
      "timeZone": "Europe/Berlin",
      "times": ["02:00", "05:00", "08:00", "11:00", "14:00", "17:00", "20:00", "23:00"],
      "durationMinutes": 0,
      "verified": false
    }
  ],
  "fieldBossMaps": {
    "1110": { "block": 2400, "name": "Альтгард" }
  },
  "respawnMinutes": {}
}
```

В `AionDpsMeter.Timers/AionDpsMeter.Timers.csproj` добавить:

```xml
  <ItemGroup>
    <EmbeddedResource Include="..\schedule.json" LogicalName="schedule.json" Link="schedule.json" />
  </ItemGroup>
```

- [ ] **Step 2: Write the failing tests**

`AionDpsMeter.Timers.Tests/TempDir.cs`:

```csharp
namespace AionDpsMeter.Timers.Tests;

internal sealed class TempDir : IDisposable
{
    public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "aion2-timers-" + Guid.NewGuid().ToString("N"));

    public TempDir() => Directory.CreateDirectory(Path);

    public string File(string name) => System.IO.Path.Combine(Path, name);

    public void Dispose()
    {
        try { Directory.Delete(Path, recursive: true); } catch (IOException) { }
    }
}
```

`AionDpsMeter.Timers.Tests/Schedule/ScheduleJsonTests.cs`:

```csharp
using AionDpsMeter.Timers.Schedule;

namespace AionDpsMeter.Timers.Tests.Schedule;

public class ScheduleJsonTests
{
    [Fact]
    public void The_shipped_schedule_parses()
    {
        var data = ScheduleJson.Parse(ScheduleSource.ReadEmbedded());
        var rift = Assert.Single(data.Events, e => e.Id == "rift");
        Assert.Equal(8, rift.Times.Count);
        Assert.False(rift.Verified);
        Assert.Equal(2400, data.FieldBossMaps[1110].Block);
    }

    [Fact]
    public void A_broken_event_is_skipped_and_the_rest_kept()
    {
        var data = ScheduleJson.Parse("""
            { "events": [
                { "id": "a", "name": "A", "kind": "fixed", "timeZone": "UTC", "times": ["12:00"] },
                { "id": "b", "name": "B", "kind": "fixed", "timeZone": "UTC", "times": ["25:99"] },
                { "id": "c", "name": "C", "kind": "fixed", "timeZone": "Mars/Olympus", "times": ["12:00"] },
                { "id": "d", "name": "D", "kind": "sometimes" }
            ] }
            """);
        Assert.Equal(["a"], data.Events.Select(e => e.Id));
        Assert.True(data.Events[0].Verified); // verified defaults to true
    }

    [Fact]
    public void Interval_events_parse()
    {
        var data = ScheduleJson.Parse("""
            { "events": [ { "id": "i", "name": "I", "kind": "interval", "anchor": "2026-10-07T00:00:00Z",
                            "periodMinutes": 90, "durationMinutes": 30 } ] }
            """);
        var e = Assert.Single(data.Events);
        Assert.Equal(ScheduleKind.Interval, e.Kind);
        Assert.Equal(new DateTimeOffset(2026, 10, 7, 0, 0, 0, TimeSpan.Zero), e.Anchor);
        Assert.Equal(TimeSpan.FromMinutes(90), e.Period);
        Assert.Equal(TimeSpan.FromMinutes(30), e.Duration);
    }

    [Fact]
    public void Invalid_json_throws_FormatException()
    {
        Assert.Throws<FormatException>(() => ScheduleJson.Parse("{ not json"));
    }
}
```

`AionDpsMeter.Timers.Tests/Schedule/ScheduleSourceTests.cs`:

```csharp
using System.Net;
using AionDpsMeter.Timers.Schedule;

namespace AionDpsMeter.Timers.Tests.Schedule;

public class ScheduleSourceTests
{
    private const string Remote = """{ "events": [ { "id": "remote", "name": "R", "kind": "fixed", "timeZone": "UTC", "times": ["12:00"] } ] }""";
    private const string Cached = """{ "events": [ { "id": "cached", "name": "C", "kind": "fixed", "timeZone": "UTC", "times": ["12:00"] } ] }""";
    private const string Embedded = """{ "events": [ { "id": "embedded", "name": "E", "kind": "fixed", "timeZone": "UTC", "times": ["12:00"] } ] }""";

    private sealed class StubHandler(Func<HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(respond());
    }

    private static ScheduleSource Source(TempDir dir, Func<HttpResponseMessage> respond) =>
        new(new HttpClient(new StubHandler(respond)), new Uri("https://example.test/schedule.json"), dir.File("cache.json"), () => Embedded);

    private static HttpResponseMessage Ok(string body) => new(HttpStatusCode.OK) { Content = new StringContent(body) };

    [Fact]
    public async Task Refresh_takes_the_remote_file_and_caches_it()
    {
        using var dir = new TempDir();
        var source = Source(dir, () => Ok(Remote));
        var changed = 0;
        source.Changed += () => changed++;

        Assert.True(await source.RefreshAsync());

        Assert.Equal("remote", source.Current.Events.Single().Id);
        Assert.Equal("remote", source.Origin);
        Assert.Equal(Remote, File.ReadAllText(dir.File("cache.json")));
        Assert.Equal(1, changed);
    }

    [Fact]
    public async Task Network_failure_keeps_what_was_loaded()
    {
        using var dir = new TempDir();
        var source = Source(dir, () => throw new HttpRequestException("offline"));
        source.LoadLocal();

        Assert.False(await source.RefreshAsync());

        Assert.Equal("embedded", source.Current.Events.Single().Id);
        Assert.Equal("embedded", source.Origin);
    }

    [Fact]
    public async Task A_broken_remote_file_does_not_overwrite_the_cache()
    {
        using var dir = new TempDir();
        File.WriteAllText(dir.File("cache.json"), Cached);
        var source = Source(dir, () => Ok("{ broken"));
        source.LoadLocal();

        Assert.False(await source.RefreshAsync());

        Assert.Equal("cached", source.Current.Events.Single().Id);
        Assert.Equal(Cached, File.ReadAllText(dir.File("cache.json")));
    }

    [Fact]
    public async Task Http_error_status_is_a_failed_refresh()
    {
        using var dir = new TempDir();
        var source = Source(dir, () => new HttpResponseMessage(HttpStatusCode.NotFound));
        Assert.False(await source.RefreshAsync());
    }

    [Fact]
    public void LoadLocal_prefers_the_cache_and_falls_back_to_embedded_when_it_is_corrupt()
    {
        using var dir = new TempDir();
        File.WriteAllText(dir.File("cache.json"), Cached);
        var source = Source(dir, () => Ok(Remote));
        source.LoadLocal();
        Assert.Equal("cached", source.Current.Events.Single().Id);

        File.WriteAllText(dir.File("cache.json"), "garbage");
        source.LoadLocal();
        Assert.Equal("embedded", source.Current.Events.Single().Id);
    }
}
```

- [ ] **Step 3: Run tests to verify they fail**

Run: `dotnet test AionDpsMeter.Timers.Tests`
Expected: FAIL — ошибка компиляции «ScheduleJson / ScheduleSource does not exist».

- [ ] **Step 4: Write the implementation**

`AionDpsMeter.Timers/EmbeddedResources.cs`:

```csharp
namespace AionDpsMeter.Timers;

internal static class EmbeddedResources
{
    public static string Read(string name)
    {
        using var stream = typeof(EmbeddedResources).Assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"{name} is not embedded in AionDpsMeter.Timers");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
```

`AionDpsMeter.Timers/Schedule/ScheduleData.cs`:

```csharp
namespace AionDpsMeter.Timers.Schedule;

/// <summary>A map whose in-game boss list names its bosses by place in this NPC code block (code / 1000).</summary>
public sealed record FieldBossMapInfo(int Block, string Name);

public sealed record ScheduleData(
    IReadOnlyList<ScheduledEvent> Events,
    IReadOnlyDictionary<int, FieldBossMapInfo> FieldBossMaps,
    IReadOnlyDictionary<int, int> RespawnMinutes)
{
    public static ScheduleData Empty { get; } =
        new([], new Dictionary<int, FieldBossMapInfo>(), new Dictionary<int, int>());
}
```

`AionDpsMeter.Timers/Schedule/ScheduleJson.cs`:

```csharp
using System.Globalization;
using System.Text.Json;

namespace AionDpsMeter.Timers.Schedule;

/// <summary>Reads schedule.json. A broken file throws; a broken event is skipped so one typo cannot hide the rest.</summary>
public static class ScheduleJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public static ScheduleData Parse(string json)
    {
        FileDto file;
        try
        {
            file = JsonSerializer.Deserialize<FileDto>(json, Options) ?? throw new FormatException("schedule.json is empty");
        }
        catch (JsonException ex)
        {
            throw new FormatException("schedule.json is not valid JSON", ex);
        }

        var events = new List<ScheduledEvent>();
        foreach (var dto in file.Events ?? [])
            if (TryEvent(dto, out var e)) events.Add(e);

        var maps = new Dictionary<int, FieldBossMapInfo>();
        foreach (var (key, map) in file.FieldBossMaps ?? new())
            if (int.TryParse(key, out var id) && map.Block > 0) maps[id] = new FieldBossMapInfo(map.Block, map.Name ?? "");

        var respawn = new Dictionary<int, int>();
        foreach (var (key, minutes) in file.RespawnMinutes ?? new())
            if (int.TryParse(key, out var code) && minutes > 0) respawn[code] = minutes;

        return new ScheduleData(events, maps, respawn);
    }

    private static bool TryEvent(EventDto dto, out ScheduledEvent e)
    {
        e = null!;
        if (string.IsNullOrWhiteSpace(dto.Id) || string.IsNullOrWhiteSpace(dto.Name)) return false;
        if (!TryZone(dto.TimeZone ?? "UTC", out var zone)) return false;
        var duration = TimeSpan.FromMinutes(Math.Max(0, dto.DurationMinutes));
        var category = dto.Category ?? "event";

        switch (dto.Kind?.ToLowerInvariant())
        {
            case "fixed":
                var times = new List<TimeOnly>();
                foreach (var t in dto.Times ?? [])
                {
                    if (!TimeOnly.TryParseExact(t, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var time)) return false;
                    times.Add(time);
                }
                if (times.Count == 0) return false;

                HashSet<DayOfWeek>? days = null;
                if (dto.Days is { Count: > 0 })
                {
                    days = new HashSet<DayOfWeek>();
                    foreach (var d in dto.Days)
                    {
                        if (!Enum.TryParse<DayOfWeek>(d, ignoreCase: true, out var day)) return false;
                        days.Add(day);
                    }
                }
                e = new ScheduledEvent(dto.Id, dto.Name, category, ScheduleKind.Fixed, zone, times, days, null, null, duration, dto.Verified);
                return true;

            case "interval":
                if (dto.PeriodMinutes is not > 0) return false;
                if (!DateTimeOffset.TryParse(dto.Anchor, CultureInfo.InvariantCulture, DateTimeStyles.None, out var anchor)) return false;
                e = new ScheduledEvent(dto.Id, dto.Name, category, ScheduleKind.Interval, zone, [], null,
                    anchor, TimeSpan.FromMinutes(dto.PeriodMinutes.Value), duration, dto.Verified);
                return true;

            default:
                return false;
        }
    }

    /// <summary>IANA ("Europe/Berlin") or Windows ids; "UTC" always works.</summary>
    internal static bool TryZone(string id, out TimeZoneInfo zone)
    {
        zone = TimeZoneInfo.Utc;
        if (id.Equals("UTC", StringComparison.OrdinalIgnoreCase)) return true;
        try
        {
            zone = TimeZoneInfo.FindSystemTimeZoneById(id);
            return true;
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException) { }

        if (TimeZoneInfo.TryConvertIanaIdToWindowsId(id, out var windowsId))
        {
            try
            {
                zone = TimeZoneInfo.FindSystemTimeZoneById(windowsId);
                return true;
            }
            catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException) { }
        }
        zone = TimeZoneInfo.Utc;
        return false;
    }

    internal sealed class FileDto
    {
        public List<EventDto>? Events { get; set; }
        public Dictionary<string, MapDto>? FieldBossMaps { get; set; }
        public Dictionary<string, int>? RespawnMinutes { get; set; }
    }

    internal sealed class EventDto
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public string? Category { get; set; }
        public string? Kind { get; set; }
        public string? TimeZone { get; set; }
        public List<string>? Times { get; set; }
        public List<string>? Days { get; set; }
        public string? Anchor { get; set; }
        public int? PeriodMinutes { get; set; }
        public int DurationMinutes { get; set; }
        public bool Verified { get; set; } = true;
    }

    internal sealed class MapDto
    {
        public int Block { get; set; }
        public string? Name { get; set; }
    }
}
```

`AionDpsMeter.Timers/Schedule/ScheduleSource.cs`:

```csharp
namespace AionDpsMeter.Timers.Schedule;

/// <summary>schedule.json from GitHub, else the last downloaded copy, else the copy built into the app.</summary>
public sealed class ScheduleSource(HttpClient http, Uri remote, string cachePath, Func<string> embedded)
{
    public const string DefaultRemote = "https://raw.githubusercontent.com/drixed/aion2-overlay/master/schedule.json";

    private volatile ScheduleData current = ScheduleData.Empty;

    public ScheduleData Current => current;

    /// <summary>"none", "embedded", "cache" or "remote".</summary>
    public string Origin { get; private set; } = "none";

    public event Action? Changed;

    public void LoadLocal()
    {
        try
        {
            if (File.Exists(cachePath))
            {
                current = ScheduleJson.Parse(File.ReadAllText(cachePath));
                Origin = "cache";
                Changed?.Invoke();
                return;
            }
        }
        catch (Exception ex) when (ex is IOException or FormatException or UnauthorizedAccessException) { }

        current = ScheduleJson.Parse(embedded());
        Origin = "embedded";
        Changed?.Invoke();
    }

    public async Task<bool> RefreshAsync(CancellationToken ct = default)
    {
        try
        {
            var json = await http.GetStringAsync(remote, ct);
            var data = ScheduleJson.Parse(json); // throws before anything is overwritten
            var tmp = cachePath + ".tmp";
            await File.WriteAllTextAsync(tmp, json, ct);
            File.Move(tmp, cachePath, overwrite: true);
            current = data;
            Origin = "remote";
            Changed?.Invoke();
            return true;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or FormatException
                                       or IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    public static string ReadEmbedded() => EmbeddedResources.Read("schedule.json");
}
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test AionDpsMeter.Timers.Tests`
Expected: PASS (все тесты Task 2 и Task 3).

- [ ] **Step 6: Commit**

```bash
git add schedule.json AionDpsMeter.Timers AionDpsMeter.Timers.Tests
git commit -m "feat(timers): schedule.json with remote, cached and embedded sources"
```

---

### Task 4: Разбор пакета со списком полевых боссов

**Files:**
- Create: `AionDpsMeter.Timers/Bosses/Wire.cs`
- Create: `AionDpsMeter.Timers/Bosses/FieldBossListParser.cs`
- Test: `AionDpsMeter.Timers.Tests/Bosses/Fixtures.cs`, `AionDpsMeter.Timers.Tests/Bosses/FieldBossListParserTests.cs`

**Interfaces:**
- Consumes: —
- Produces:
  - `record FieldBossSlot(int SlotId, bool Alive, long AtMs, float X, float Y, float Z)`
  - `record FieldBossList(int MapId, int Count, IReadOnlyList<FieldBossSlot> Slots)` с `bool Complete` и `bool SameAs(FieldBossList other)`
  - `static FieldBossList? FieldBossListParser.Parse(ReadOnlySpan<byte> body)` — `body` = байты после опкода; `const ushort FieldBossListParser.Opcode = 0x9101`
  - `internal static bool Wire.TryVarint(ReadOnlySpan<byte> b, int offset, out long value, out int length)`
  - Тестовые константы `Fixtures.AltgardList`, `Fixtures.List20` (hex тела пакета).

- [ ] **Step 1: Write the failing tests**

`AionDpsMeter.Timers.Tests/Bosses/Fixtures.cs` (образцы — живые EU-пакеты из тестов cyberbadger6969/aion2-dps-meter, GPL-3.0):

```csharp
namespace AionDpsMeter.Timers.Tests.Bosses;

internal static class Fixtures
{
    /// <summary>Altgard (map 1110), 24 field bosses, captured on EU 2026-10-05/06. Body after the opcode.</summary>
    public const string AltgardList =
        "00005604000018009de30602729d660ea10100000199e30691939bc7645092c700ecdd467f04f30da1010000009fe3066d013f0ea1010000" +
        "00a0e3066efe360ea1010000009ae306c4e9010ea1010000009be306a54a140ea1010000009ee306bc74360ea1010000009ce30630852c0e" +
        "a101000001a1e30600a57447800a0dc800f0db45095a05d40da101000000a2e3061f365b0ea101000000a3e306d4eb670ea101000001a4e3" +
        "06413f42482dc5e5c700b6974683a4f40da101000000ace30650d2de0ea101000000a5e3068054070ea101000000a6e306dae65e0ea10100" +
        "0000a7e3061737050ea101000000a8e306001166e80ea101000000a9e306ef5f4d0ea101000000aae30668a8020ea101000000abe306c284" +
        "d30ea101000000ade306fbccc10ea101000000aee3061cfdc60ea101000000afe306bf00d70ea101000000b0e3063501d10ea10100000000" +
        "00";

    /// <summary>Map 20, 8 scheduled bosses (2026-10-06 01:07): slot 2001 carries the extra byte, slot 2002 has no time.</summary>
    public const string List20 =
        "000014000000080" + "0d10f00f187f20ea1010000" + "00d20f0000000000000000" + "00d30f60eb0d22a1010000" +
        "00d40f60eb0d22a1010000" + "00d50f60eb0d22a1010000" + "00d60fa006031da1010000" + "00d80fa006031da1010000" +
        "00d70fa006031da1010000" + "000000";

    public static byte[] Bytes(string hex) => Convert.FromHexString(hex);
}
```

`AionDpsMeter.Timers.Tests/Bosses/FieldBossListParserTests.cs`:

```csharp
using AionDpsMeter.Timers.Bosses;

namespace AionDpsMeter.Timers.Tests.Bosses;

public class FieldBossListParserTests
{
    private static DateTime Trim(long ms)
    {
        var t = DateTimeOffset.FromUnixTimeMilliseconds(ms).UtcDateTime;
        return new DateTime(t.Year, t.Month, t.Day, t.Hour, t.Minute, t.Second);
    }

    [Fact]
    public void Altgard_list_decodes_every_slot()
    {
        var list = FieldBossListParser.Parse(Fixtures.Bytes(Fixtures.AltgardList))!;
        Assert.Equal(1110, list.MapId);
        Assert.True(list.Complete);
        Assert.Equal(Enumerable.Range(111001, 24), list.Slots.Select(s => s.SlotId).Order());
        Assert.Equal([111001, 111009, 111012], list.Slots.Where(s => s.Alive).Select(s => s.SlotId));

        var danar = list.Slots.Single(s => s.SlotId == 111001);
        Assert.InRange(danar.X, -79656f, -79654f);
        Assert.Equal(new DateTime(2026, 10, 5, 21, 23, 12), Trim(danar.AtMs));
        Assert.Equal(new DateTime(2026, 10, 6, 1, 9, 4), Trim(list.Slots.Single(s => s.SlotId == 111021).AtMs));
        Assert.Equal(new DateTime(2026, 10, 5, 23, 29, 28), Trim(list.Slots.Single(s => s.SlotId == 111005).AtMs)); // extra byte
    }

    [Fact]
    public void Slots_without_a_time_decode()
    {
        var list = FieldBossListParser.Parse(Fixtures.Bytes(Fixtures.List20))!;
        Assert.True(list.Complete);
        Assert.Equal(20, list.MapId);
        Assert.Equal([2001, 2002, 2003, 2004, 2005, 2006, 2008, 2007], list.Slots.Select(s => s.SlotId));
        Assert.Equal(0, list.Slots[1].AtMs);
        Assert.Equal(new DateTimeOffset(2026, 10, 9, 19, 5, 0, TimeSpan.Zero), DateTimeOffset.FromUnixTimeMilliseconds(list.Slots[2].AtMs));
    }

    [Fact]
    public void Truncated_list_returns_the_slots_read_so_far()
    {
        var bytes = Fixtures.Bytes(Fixtures.AltgardList);
        var list = FieldBossListParser.Parse(bytes.AsSpan(0, 60))!;
        Assert.False(list.Complete);
        Assert.Equal(24, list.Count);
        Assert.True(list.Slots.Count < 24);
    }

    [Fact]
    public void Garbage_never_throws()
    {
        Assert.Null(FieldBossListParser.Parse([1, 2, 3]));
        var random = new Random(42);
        for (var i = 0; i < 2000; i++)
        {
            var junk = new byte[random.Next(0, 300)];
            random.NextBytes(junk);
            _ = FieldBossListParser.Parse(junk); // must not throw
        }
    }

    [Fact]
    public void SameAs_compares_the_slots()
    {
        var a = FieldBossListParser.Parse(Fixtures.Bytes(Fixtures.AltgardList))!;
        var b = FieldBossListParser.Parse(Fixtures.Bytes(Fixtures.AltgardList))!;
        var c = FieldBossListParser.Parse(Fixtures.Bytes(Fixtures.List20))!;
        Assert.True(a.SameAs(b));
        Assert.False(a.SameAs(c));
    }

    [Fact]
    public void Varint_reads_leb128()
    {
        Assert.True(Wire.TryVarint([0x99, 0xE3, 0x06], 0, out var v, out var len));
        Assert.Equal(111001, v);
        Assert.Equal(3, len);
        Assert.False(Wire.TryVarint([0x80, 0x80], 0, out _, out _)); // unterminated
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test AionDpsMeter.Timers.Tests`
Expected: FAIL — ошибка компиляции «AionDpsMeter.Timers.Bosses does not exist».

- [ ] **Step 3: Write the implementation**

`AionDpsMeter.Timers/Bosses/Wire.cs`:

```csharp
namespace AionDpsMeter.Timers.Bosses;

internal static class Wire
{
    /// <summary>LEB128 varint at <paramref name="offset"/>.</summary>
    public static bool TryVarint(ReadOnlySpan<byte> b, int offset, out long value, out int length)
    {
        value = 0;
        length = 0;
        var shift = 0;
        while (offset + length < b.Length && length < 10)
        {
            var x = b[offset + length++];
            value |= (long)(x & 0x7F) << shift;
            if ((x & 0x80) == 0) return true;
            shift += 7;
        }
        value = 0;
        length = 0;
        return false;
    }
}
```

`AionDpsMeter.Timers/Bosses/FieldBossListParser.cs`:

```csharp
using System.Buffers.Binary;

namespace AionDpsMeter.Timers.Bosses;

/// <param name="SlotId">Map id × 100 + the boss's place in the map's list (bosses sorted by NPC code).</param>
/// <param name="AtMs">Unix ms: alive since (alive) / back at (dead); 0 when the game keeps no time.</param>
public sealed record FieldBossSlot(int SlotId, bool Alive, long AtMs, float X, float Y, float Z);

public sealed record FieldBossList(int MapId, int Count, IReadOnlyList<FieldBossSlot> Slots)
{
    public bool Complete => Slots.Count == Count;

    public bool SameAs(FieldBossList other) =>
        MapId == other.MapId && Count == other.Count && Slots.SequenceEqual(other.Slots);
}

/// <summary>
/// The in-game map's field boss list (wire bytes <c>01 91</c>), sent about once a second while the map is open.
/// Layout ported from cyberbadger6969/aion2-dps-meter (GPL-3.0):
/// <c>u16 0, map u32, count u8, count × { alive u8, slot varint, [alive: x y z f32], [u8 on some slots], time i64 ms }, 00…</c>
/// The optional byte is resolved by reading on: only one of the two readings leaves the next slot where it must be.
/// </summary>
public static class FieldBossListParser
{
    /// <summary>RATmeter reads opcodes little-endian: wire bytes 01 91 → 0x9101.</summary>
    public const ushort Opcode = 0x9101;

    public static FieldBossList? Parse(ReadOnlySpan<byte> b)
    {
        if (b.Length < 7) return null;
        var map = (int)BinaryPrimitives.ReadUInt32LittleEndian(b.Slice(2, 4));
        if (map <= 0) return null;
        int count = b[6];
        var slots = new List<FieldBossSlot>(count);
        var o = 7;
        for (var n = 0; n < count && TrySlot(b, ref o, map, last: n == count - 1, out var slot); n++)
            slots.Add(slot);
        return new FieldBossList(map, count, slots);
    }

    private static bool TrySlot(ReadOnlySpan<byte> b, ref int o, int map, bool last, out FieldBossSlot slot)
    {
        slot = null!;
        if (!SlotHeader(b, o, map, out var alive, out var id, out var at)) return false;
        float x = 0, y = 0, z = 0;
        if (alive)
        {
            if (at + 12 > b.Length) return false;
            x = BinaryPrimitives.ReadSingleLittleEndian(b.Slice(at, 4));
            y = BinaryPrimitives.ReadSingleLittleEndian(b.Slice(at + 4, 4));
            z = BinaryPrimitives.ReadSingleLittleEndian(b.Slice(at + 8, 4));
            at += 12;
        }
        for (var extra = 0; extra <= 1; extra++)
        {
            var t = at + extra;
            if (t + 8 > b.Length) break;
            var time = BinaryPrimitives.ReadInt64LittleEndian(b.Slice(t, 8));
            if (time != 0 && time is < 1_600_000_000_000 or > 2_600_000_000_000) continue;
            var end = t + 8;
            var fits = last
                ? b[end..].IndexOfAnyExcept((byte)0) < 0 && b.Length - end <= 8
                : SlotHeader(b, end, map, out _, out _, out _);
            if (!fits) continue;
            slot = new FieldBossSlot(id, alive, time, x, y, z);
            o = end;
            return true;
        }
        return false;
    }

    /// <summary><c>alive u8 (0 / 1), slot varint</c> with the slot inside this map's range.</summary>
    private static bool SlotHeader(ReadOnlySpan<byte> b, int o, int map, out bool alive, out int slot, out int next)
    {
        alive = false;
        slot = 0;
        next = o;
        if (o >= b.Length || b[o] > 1) return false;
        alive = b[o] == 1;
        if (!Wire.TryVarint(b, o + 1, out var v, out var len)) return false;
        if (v <= (long)map * 100 || v >= (long)map * 100 + 100) return false;
        slot = (int)v;
        next = o + 1 + len;
        return true;
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test AionDpsMeter.Timers.Tests`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add AionDpsMeter.Timers AionDpsMeter.Timers.Tests
git commit -m "feat(timers): field boss list packet parser (ported from cyberbadger meter)"
```

---

### Task 5: Справочник боссов (имена, блоки кодов)

**Files:**
- Create: `AionDpsMeter.Timers/Data/npcs.ru.json` (копия `data/npcs/ru.json` из cyberbadger6969/aion2-dps-meter, коммит `535edf3c6594e07393ee52a0af8bc28a564651f0`)
- Create: `AionDpsMeter.Timers/Data/NOTICE.md`
- Create: `AionDpsMeter.Timers/Bosses/BossCatalog.cs`
- Modify: `AionDpsMeter.Timers/AionDpsMeter.Timers.csproj` (встроить `npcs.ru.json`)
- Test: `AionDpsMeter.Timers.Tests/Bosses/BossCatalogTests.cs`

**Interfaces:**
- Consumes: `EmbeddedResources.Read` (Task 3).
- Produces: `class BossCatalog`: `static BossCatalog Empty`, `static BossCatalog FromJson(params string[] tables)`, `static BossCatalog LoadDefault()`, `bool IsBoss(int code)`, `string Name(int code)`, `IReadOnlyList<int> FieldBossesInBlock(int block)`, `int BossInSlot(int block, int mapId, int slotId, int slotCount)`.

- [ ] **Step 1: Скачать таблицу NPC с русскими именами и встроить её**

```bash
mkdir -p AionDpsMeter.Timers/Data
curl -fsSL -o AionDpsMeter.Timers/Data/npcs.ru.json https://raw.githubusercontent.com/cyberbadger6969/aion2-dps-meter/535edf3c6594e07393ee52a0af8bc28a564651f0/data/npcs/ru.json
head -c 200 AionDpsMeter.Timers/Data/npcs.ru.json
```
Expected: начинается с `{` и `"2000000": {` и `"name": "Мешок с песком"`.

`AionDpsMeter.Timers/Data/NOTICE.md`:

```markdown
`npcs.ru.json` — таблица NPC (код → имя, isBoss, isDummy) с русскими именами. Взята из
[cyberbadger6969/aion2-dps-meter](https://github.com/cyberbadger6969/aion2-dps-meter) `data/npcs/ru.json`,
коммит `535edf3`, GPL-3.0; там она в свою очередь из [taengu/A2Tools-DPS-Meter](https://github.com/taengu/A2Tools-DPS-Meter).
Данные игры © NCSOFT.
```

В `AionDpsMeter.Timers/AionDpsMeter.Timers.csproj`, в `ItemGroup` с `schedule.json`, добавить:

```xml
    <EmbeddedResource Include="Data\npcs.ru.json" LogicalName="npcs.ru.json" />
```

- [ ] **Step 2: Write the failing tests**

`AionDpsMeter.Timers.Tests/Bosses/BossCatalogTests.cs`:

```csharp
using AionDpsMeter.Timers.Bosses;

namespace AionDpsMeter.Timers.Tests.Bosses;

public class BossCatalogTests
{
    private const string Ru = """
        { "2400017": { "name": "Данар", "isBoss": true },
          "2400800": { "name": "Гартуа", "isBoss": true },
          "2400500": { "name": "Чучело", "isBoss": true, "isDummy": true },
          "2000002": { "name": "Драконид", "isBoss": false } }
        """;

    private const string Rat = """
        { "2400017": { "name": "Danar", "isBoss": true },
          "2400999": { "name": "New boss in an old block", "isBoss": true },
          "2600001": { "name": "Fresh boss", "isBoss": true } }
        """;

    [Fact]
    public void Knows_bosses_and_names()
    {
        var c = BossCatalog.FromJson(Ru);
        Assert.True(c.IsBoss(2400017));
        Assert.False(c.IsBoss(2400500)); // training dummy
        Assert.False(c.IsBoss(2000002));
        Assert.Equal("Данар", c.Name(2400017));
        Assert.Equal("Босс 123", c.Name(123));
    }

    [Fact]
    public void First_table_wins_names_and_later_tables_add_new_codes()
    {
        var c = BossCatalog.FromJson(Ru, Rat);
        Assert.Equal("Данар", c.Name(2400017));
        Assert.True(c.IsBoss(2600001));
        Assert.Equal("Fresh boss", c.Name(2600001));
    }

    [Fact]
    public void A_block_is_counted_from_the_first_table_that_has_bosses_in_it()
    {
        var c = BossCatalog.FromJson(Ru, Rat);
        Assert.Equal([2400017, 2400800], c.FieldBossesInBlock(2400));
        Assert.Equal([2600001], c.FieldBossesInBlock(2600));
    }

    [Fact]
    public void BossInSlot_needs_the_block_size_to_match_the_list()
    {
        var c = BossCatalog.FromJson(Ru);
        Assert.Equal(2400800, c.BossInSlot(2400, 1110, 111002, 2));
        Assert.Equal(0, c.BossInSlot(2400, 1110, 111002, 3));
        Assert.Equal(0, c.BossInSlot(2400, 1110, 111003, 2));
    }

    [Fact]
    public void A_broken_table_is_skipped()
    {
        var c = BossCatalog.FromJson("{ broken", Ru);
        Assert.True(c.IsBoss(2400017));
    }

    [Fact]
    public void Shipped_table_resolves_the_Altgard_list()
    {
        var c = BossCatalog.FromJson(EmbeddedResources.Read("npcs.ru.json"));
        Assert.Equal(24, c.FieldBossesInBlock(2400).Count);
        Assert.Equal(2400800, c.BossInSlot(2400, 1110, 111021, 24));
        Assert.Equal(2400017, c.BossInSlot(2400, 1110, 111001, 24));
        Assert.NotEqual("Босс 2400800", c.Name(2400800));
    }
}
```

- [ ] **Step 3: Run tests to verify they fail**

Run: `dotnet test AionDpsMeter.Timers.Tests`
Expected: FAIL — ошибка компиляции «BossCatalog does not exist».

- [ ] **Step 4: Write the implementation**

`AionDpsMeter.Timers/Bosses/BossCatalog.cs`:

```csharp
using System.Text.Json;

namespace AionDpsMeter.Timers.Bosses;

/// <summary>
/// NPC table: which codes are bosses and what they are called. Built from several tables: the first one that knows a
/// code gives its name; later ones only add codes the earlier ones lack (so new bosses from upstream's mobs.json show up).
/// </summary>
public sealed class BossCatalog
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    private readonly Dictionary<int, Npc> npcs;
    private readonly Dictionary<int, List<int>> blocks = new();
    private readonly object gate = new();

    private sealed record Npc(string Name, bool IsBoss, bool IsDummy, int Source);

    private BossCatalog(Dictionary<int, Npc> npcs) => this.npcs = npcs;

    public static BossCatalog Empty { get; } = new(new());

    public static BossCatalog FromJson(params string[] tables)
    {
        var npcs = new Dictionary<int, Npc>();
        for (var source = 0; source < tables.Length; source++)
        {
            Dictionary<string, NpcDto>? table;
            try
            {
                table = JsonSerializer.Deserialize<Dictionary<string, NpcDto>>(tables[source], Options);
            }
            catch (JsonException)
            {
                continue;
            }
            foreach (var (key, dto) in table ?? new())
            {
                if (!int.TryParse(key, out var code) || npcs.ContainsKey(code)) continue;
                npcs[code] = new Npc(dto.Name ?? "", dto.IsBoss, dto.IsDummy, source);
            }
        }
        return new BossCatalog(npcs);
    }

    /// <summary>The Russian table built into the app, then RATmeter's own mobs.json (kept fresh by upstream).</summary>
    public static BossCatalog LoadDefault()
    {
        var tables = new List<string> { EmbeddedResources.Read("npcs.ru.json") };
        var upstreamMobs = Path.Combine(AppContext.BaseDirectory, "GameData", "Assets", "mobs.json");
        try
        {
            if (File.Exists(upstreamMobs)) tables.Add(File.ReadAllText(upstreamMobs));
        }
        catch (IOException) { }
        return FromJson(tables.ToArray());
    }

    public bool IsBoss(int code) => npcs.TryGetValue(code, out var n) && n.IsBoss && !n.IsDummy;

    public string Name(int code) => npcs.TryGetValue(code, out var n) && n.Name.Length > 0 ? n.Name : $"Босс {code}";

    /// <summary>
    /// A map's field bosses share one block of a thousand NPC codes, and the in-game list shows them in code order.
    /// Counted from the first table that has bosses in the block, so a second table cannot shift the places.
    /// </summary>
    public IReadOnlyList<int> FieldBossesInBlock(int block)
    {
        lock (gate)
        {
            if (blocks.TryGetValue(block, out var cached)) return cached;
            var bosses = npcs.Where(kv => kv.Value.IsBoss && !kv.Value.IsDummy && kv.Key / 1000 == block).ToList();
            var source = bosses.Count == 0 ? 0 : bosses.Min(kv => kv.Value.Source);
            var codes = bosses.Where(kv => kv.Value.Source == source).Select(kv => kv.Key).Order().ToList();
            blocks[block] = codes;
            return codes;
        }
    }

    /// <summary>The boss in a list slot (map × 100 + place) when the block holds exactly as many bosses as the list; else 0.</summary>
    public int BossInSlot(int block, int mapId, int slotId, int slotCount)
    {
        var codes = FieldBossesInBlock(block);
        var place = slotId - mapId * 100;
        return codes.Count == slotCount && place >= 1 && place <= codes.Count ? codes[place - 1] : 0;
    }

    internal sealed class NpcDto
    {
        public string? Name { get; set; }
        public bool IsBoss { get; set; }
        public bool IsDummy { get; set; }
    }
}
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test AionDpsMeter.Timers.Tests`
Expected: PASS. Если `Shipped_table_resolves_the_Altgard_list` падает на количестве 24 — таблица скачана не та (проверить коммит в URL), а не «подогнать» тест.

- [ ] **Step 6: Commit**

```bash
git add AionDpsMeter.Timers AionDpsMeter.Timers.Tests
git commit -m "feat(timers): boss catalog with Russian names"
```

---

### Task 6: Трекер полевых боссов и сохранение состояния

**Files:**
- Create: `AionDpsMeter.Timers/Bosses/FieldBossTracker.cs`
- Create: `AionDpsMeter.Timers/Bosses/TimerStateStore.cs`
- Test: `AionDpsMeter.Timers.Tests/Bosses/FieldBossTrackerTests.cs`

**Interfaces:**
- Consumes: `BossCatalog` (Task 5), `FieldBossList`/`FieldBossSlot` (Task 4), `ScheduleData`/`FieldBossMapInfo` (Task 3).
- Produces:
  - `record BossTimer(int ServerId, int Key, int MapId, int SlotId, bool Alive, DateTimeOffset? AliveSince, DateTimeOffset? NextSpawn, DateTimeOffset? LastKill, DateTimeOffset? ListedAt)` — `Key` = код NPC или `-SlotId`, пока босс слота неизвестен.
  - `record TrackerState(List<BossTimer> Timers, Dictionary<int, int> LearnedBlocks, Dictionary<int, int> LearnedRespawnMinutes)`
  - `class FieldBossTracker(BossCatalog catalog, Func<ScheduleData> data, TimeProvider time)`: `event Action? Changed`, `IReadOnlyList<BossTimer> TimersFor(int serverId)`, `int RespawnMinutesOf(int code)`, `void OnList(int serverId, FieldBossList list)`, `void OnKill(int serverId, int code)`, `void MarkKilled(int serverId, int key)`, `TrackerState Export()`, `void Import(TrackerState? state)`
  - `class TimerStateStore(string path)`: `TrackerState? Load()`, `void Save(TrackerState state)`

- [ ] **Step 1: Write the failing tests**

`AionDpsMeter.Timers.Tests/Bosses/FieldBossTrackerTests.cs`:

```csharp
using AionDpsMeter.Timers.Bosses;
using AionDpsMeter.Timers.Schedule;
using Microsoft.Extensions.Time.Testing;

namespace AionDpsMeter.Timers.Tests.Bosses;

public class FieldBossTrackerTests
{
    // Block 2400: three field bosses (code order: 2400017, 2400800, 2400900). Block 2500: two, map unknown.
    private static readonly BossCatalog Catalog = BossCatalog.FromJson("""
        { "2400017": { "name": "Данар", "isBoss": true },
          "2400800": { "name": "Гартуа", "isBoss": true },
          "2400900": { "name": "Третий", "isBoss": true },
          "2500001": { "name": "Чужой", "isBoss": true },
          "2500002": { "name": "Чужой 2", "isBoss": true },
          "2000002": { "name": "Моб", "isBoss": false } }
        """);

    private static readonly ScheduleData Data = new([],
        new Dictionary<int, FieldBossMapInfo> { [1110] = new(2400, "Альтгард") },
        new Dictionary<int, int>());

    private readonly FakeTimeProvider time = new(new DateTimeOffset(2026, 10, 7, 12, 0, 0, TimeSpan.Zero));

    private FieldBossTracker Tracker(ScheduleData? data = null) => new(Catalog, () => data ?? Data, time);

    private static FieldBossSlot Slot(int map, int place, bool alive, DateTimeOffset? at) =>
        new(map * 100 + place, alive, at?.ToUnixTimeMilliseconds() ?? 0, 0, 0, 0);

    private static FieldBossList List(int map, int count, params FieldBossSlot[] slots) => new(map, count, slots);

    [Fact]
    public void A_list_of_a_known_map_names_its_slots()
    {
        var t = Tracker();
        var back = time.GetUtcNow().AddHours(2);
        t.OnList(1, List(1110, 3, Slot(1110, 1, true, time.GetUtcNow().AddMinutes(-5)), Slot(1110, 2, false, back)));

        var timers = t.TimersFor(1);
        Assert.True(timers.Single(x => x.Key == 2400017).Alive);
        Assert.Equal(back, timers.Single(x => x.Key == 2400800).NextSpawn);
    }

    [Fact]
    public void Slots_of_an_unknown_map_are_kept_under_their_slot_id()
    {
        var t = Tracker();
        t.OnList(1, List(77, 2, Slot(77, 1, false, time.GetUtcNow().AddHours(1))));
        Assert.Equal(-7701, Assert.Single(t.TimersFor(1)).Key);
    }

    [Fact]
    public void Servers_do_not_share_timers()
    {
        var t = Tracker();
        t.OnList(1, List(1110, 3, Slot(1110, 1, true, time.GetUtcNow())));
        Assert.Single(t.TimersFor(1));
        Assert.Empty(t.TimersFor(2));
    }

    [Fact]
    public void A_kill_followed_by_the_list_teaches_the_respawn_interval()
    {
        var t = Tracker();
        var killedAt = time.GetUtcNow();
        t.OnKill(1, 2400800);
        time.Advance(TimeSpan.FromMinutes(10));
        t.OnList(1, List(1110, 3, Slot(1110, 2, false, killedAt.AddMinutes(120))));
        Assert.Equal(120, t.RespawnMinutesOf(2400800));

        time.Advance(TimeSpan.FromHours(3));
        t.OnKill(1, 2400800);
        Assert.Equal(time.GetUtcNow().AddMinutes(120), t.TimersFor(1).Single(x => x.Key == 2400800).NextSpawn);
    }

    [Fact]
    public void A_kill_without_a_known_interval_has_no_spawn_time_until_data_gives_one()
    {
        var t = Tracker();
        t.OnKill(1, 2400017);
        Assert.Null(t.TimersFor(1).Single().NextSpawn);

        var withData = Tracker(Data with { RespawnMinutes = new Dictionary<int, int> { [2400017] = 60 } });
        withData.OnKill(1, 2400017);
        Assert.Equal(time.GetUtcNow().AddMinutes(60), withData.TimersFor(1).Single().NextSpawn);
    }

    [Fact]
    public void The_same_kill_seen_twice_counts_once()
    {
        var t = Tracker();
        t.OnKill(1, 2400017);
        var first = t.TimersFor(1).Single().LastKill;
        time.Advance(TimeSpan.FromSeconds(30));
        t.OnKill(1, 2400017);
        Assert.Equal(first, t.TimersFor(1).Single().LastKill);
    }

    [Fact]
    public void Kills_of_non_field_bosses_and_non_bosses_are_ignored()
    {
        var t = Tracker();
        t.OnKill(1, 2500001); // boss, but its block belongs to no known or listed map (e.g. a dungeon boss)
        t.OnKill(1, 2000002); // not a boss
        Assert.Empty(t.TimersFor(1));
    }

    [Fact]
    public void A_kill_teaches_the_block_of_a_recently_listed_map()
    {
        var t = Tracker();
        t.OnList(1, List(77, 2, Slot(77, 1, true, time.GetUtcNow()), Slot(77, 2, true, time.GetUtcNow())));
        time.Advance(TimeSpan.FromMinutes(2));
        t.OnKill(1, 2500002);

        var timers = t.TimersFor(1);
        Assert.All(timers, x => Assert.True(x.Key > 0));
        var killed = timers.Single(x => x.Key == 2500002);
        Assert.False(killed.Alive);
        Assert.NotNull(killed.LastKill);
        Assert.True(timers.Single(x => x.Key == 2500001).Alive);
    }

    [Fact]
    public void MarkKilled_works_on_tracked_entries_only()
    {
        var t = Tracker();
        t.OnList(1, List(77, 2, Slot(77, 1, true, time.GetUtcNow())));
        t.MarkKilled(1, -7701);
        t.MarkKilled(1, 999);
        var only = Assert.Single(t.TimersFor(1));
        Assert.False(only.Alive);
        Assert.NotNull(only.LastKill);
    }

    [Fact]
    public void An_identical_list_changes_nothing()
    {
        var t = Tracker();
        var changes = 0;
        t.Changed += () => changes++;
        var list = List(1110, 3, Slot(1110, 1, true, time.GetUtcNow()));
        t.OnList(1, list);
        t.OnList(1, List(1110, 3, Slot(1110, 1, true, time.GetUtcNow())));
        Assert.Equal(1, changes);
    }

    [Fact]
    public void State_survives_a_restart()
    {
        using var dir = new TempDir();
        var store = new TimerStateStore(dir.File("timers-state.json"));
        var t = Tracker();
        t.OnList(1, List(77, 2, Slot(77, 1, true, time.GetUtcNow()), Slot(77, 2, true, time.GetUtcNow())));
        t.OnKill(1, 2500002); // learns block 2500 for map 77
        t.OnKill(1, 2400800);
        time.Advance(TimeSpan.FromMinutes(5));
        t.OnList(1, List(1110, 3, Slot(1110, 2, false, time.GetUtcNow().AddMinutes(115))));
        store.Save(t.Export());

        var restored = Tracker();
        restored.Import(store.Load());

        Assert.Equal(t.TimersFor(1).OrderBy(x => x.Key), restored.TimersFor(1).OrderBy(x => x.Key));
        Assert.Equal(120, restored.RespawnMinutesOf(2400800));
        restored.OnList(1, List(77, 2, Slot(77, 1, false, time.GetUtcNow().AddHours(1))));
        Assert.Contains(restored.TimersFor(1), x => x.Key == 2500001); // learned block came back too
    }

    [Fact]
    public void A_missing_or_corrupt_state_file_loads_as_nothing()
    {
        using var dir = new TempDir();
        var store = new TimerStateStore(dir.File("timers-state.json"));
        Assert.Null(store.Load());
        File.WriteAllText(dir.File("timers-state.json"), "{ nope");
        Assert.Null(store.Load());
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test AionDpsMeter.Timers.Tests`
Expected: FAIL — ошибка компиляции «FieldBossTracker / TimerStateStore does not exist».

- [ ] **Step 3: Write the implementation**

`AionDpsMeter.Timers/Bosses/FieldBossTracker.cs`:

```csharp
using AionDpsMeter.Timers.Schedule;

namespace AionDpsMeter.Timers.Bosses;

/// <param name="Key">The NPC code, or -<paramref name="SlotId"/> while the list slot's boss is not known yet.</param>
public sealed record BossTimer(
    int ServerId,
    int Key,
    int MapId,
    int SlotId,
    bool Alive,
    DateTimeOffset? AliveSince,
    DateTimeOffset? NextSpawn,
    DateTimeOffset? LastKill,
    DateTimeOffset? ListedAt);

public sealed record TrackerState(
    List<BossTimer> Timers,
    Dictionary<int, int> LearnedBlocks,
    Dictionary<int, int> LearnedRespawnMinutes);

/// <summary>
/// Field boss respawn timers, per server. Sources, newest wins: the in-game map list (the server's own times),
/// a boss seen dying (kill + respawn interval), the user's "killed" button. Thread-safe: lists arrive on the packet
/// thread, kills on the watcher, button presses and reads on the UI thread.
/// </summary>
public sealed class FieldBossTracker(BossCatalog catalog, Func<ScheduleData> data, TimeProvider time)
{
    private static readonly TimeSpan SameKill = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan ListMemory = TimeSpan.FromMinutes(10);

    private readonly object gate = new();
    private readonly Dictionary<(int Server, int Key), BossTimer> timers = new();
    private readonly Dictionary<int, int> learnedBlocks = new();   // map → NPC code block
    private readonly Dictionary<int, int> respawnMinutes = new();  // NPC code → learned interval
    private readonly Dictionary<(int Server, int Map), (FieldBossList List, DateTimeOffset At)> lastLists = new();

    public event Action? Changed;

    public IReadOnlyList<BossTimer> TimersFor(int serverId)
    {
        lock (gate) return timers.Values.Where(t => t.ServerId == serverId).ToList();
    }

    public int RespawnMinutesOf(int code)
    {
        lock (gate) return RespawnOf(code);
    }

    public void OnList(int serverId, FieldBossList list)
    {
        lock (gate)
        {
            // Sent about once a second while the map is open: an unchanged list changes nothing.
            if (lastLists.TryGetValue((serverId, list.MapId), out var previous) && previous.List.SameAs(list)) return;
            var now = time.GetUtcNow();
            lastLists[(serverId, list.MapId)] = (list, now);
            Apply(serverId, list, now);
        }
        Changed?.Invoke();
    }

    /// <summary>A boss died in view. Counted only for field bosses: its block belongs to a field map, it is already
    /// tracked, or a recent list of an unnamed map matches its block.</summary>
    public void OnKill(int serverId, int code)
    {
        if (!catalog.IsBoss(code)) return;
        lock (gate)
        {
            var now = time.GetUtcNow();
            if (!timers.ContainsKey((serverId, code)) && !IsFieldBlock(code / 1000) && !TryLearnBlock(serverId, code, now)) return;
            if (!RecordKill(serverId, code, now, manual: false)) return;
        }
        Changed?.Invoke();
    }

    /// <summary>The user says a tracked boss is dead (the overlay's "убит" button).</summary>
    public void MarkKilled(int serverId, int key)
    {
        lock (gate)
        {
            if (!timers.ContainsKey((serverId, key))) return;
            RecordKill(serverId, key, time.GetUtcNow(), manual: true);
        }
        Changed?.Invoke();
    }

    public TrackerState Export()
    {
        lock (gate) return new TrackerState(timers.Values.ToList(), new(learnedBlocks), new(respawnMinutes));
    }

    public void Import(TrackerState? state)
    {
        if (state is null) return;
        lock (gate)
        {
            foreach (var t in state.Timers ?? []) timers[(t.ServerId, t.Key)] = t;
            foreach (var (map, block) in state.LearnedBlocks ?? new()) learnedBlocks[map] = block;
            foreach (var (code, minutes) in state.LearnedRespawnMinutes ?? new()) respawnMinutes[code] = minutes;
        }
        Changed?.Invoke();
    }

    private void Apply(int serverId, FieldBossList list, DateTimeOffset now)
    {
        var block = BlockOf(list.MapId);
        foreach (var slot in list.Slots)
        {
            var code = block != 0 ? catalog.BossInSlot(block, list.MapId, slot.SlotId, list.Count) : 0;
            var key = code != 0 ? code : -slot.SlotId;
            if (code != 0 && timers.Remove((serverId, -slot.SlotId), out var unnamed) && !timers.ContainsKey((serverId, key)))
                timers[(serverId, key)] = unnamed with { Key = key }; // listed before its boss was known
            var old = timers.GetValueOrDefault((serverId, key));
            DateTimeOffset? at = slot.AtMs > 0 ? DateTimeOffset.FromUnixTimeMilliseconds(slot.AtMs) : null;
            if (code != 0 && !slot.Alive && at is { } back) LearnRespawn(code, old, back, now);
            timers[(serverId, key)] = new BossTimer(serverId, key, list.MapId, slot.SlotId, slot.Alive,
                AliveSince: slot.Alive ? at : null,
                NextSpawn: slot.Alive ? null : at,
                LastKill: old?.LastKill,
                ListedAt: now);
        }
    }

    /// <summary>A boss that just went down shows its comeback time: with a fresh death that gives the interval.</summary>
    private void LearnRespawn(int code, BossTimer? old, DateTimeOffset back, DateTimeOffset now)
    {
        DateTimeOffset? died = null;
        if (old?.LastKill is { } kill && kill <= now && now - kill < TimeSpan.FromMinutes(30) && back > kill) died = kill;
        else if (old is { Alive: true, ListedAt: { } listed } && now - listed < TimeSpan.FromMinutes(3)) died = now;
        if (died is not { } d) return;
        var minutes = (int)Math.Round((back - d).TotalMinutes / 5.0) * 5;
        if (minutes is >= 5 and <= 48 * 60) respawnMinutes[code] = minutes;
    }

    private bool RecordKill(int serverId, int key, DateTimeOffset now, bool manual)
    {
        var old = timers.GetValueOrDefault((serverId, key));
        if (!manual && old?.LastKill is { } last && now - last < SameKill) return false;
        var minutes = key > 0 ? RespawnOf(key) : 0;
        timers[(serverId, key)] = new BossTimer(serverId, key, old?.MapId ?? 0, old?.SlotId ?? 0, Alive: false,
            AliveSince: null,
            NextSpawn: minutes > 0 ? now.AddMinutes(minutes) : null,
            LastKill: now,
            ListedAt: old?.ListedAt);
        return true;
    }

    /// <summary>A boss from an unnamed block died: if exactly one recent list of a map with no known block has as many
    /// slots as that block has bosses, that map is the block's.</summary>
    private bool TryLearnBlock(int serverId, int code, DateTimeOffset now)
    {
        var block = code / 1000;
        var size = catalog.FieldBossesInBlock(block).Count;
        if (size == 0) return false;
        var candidates = lastLists
            .Where(kv => kv.Key.Server == serverId && now - kv.Value.At <= ListMemory
                         && BlockOf(kv.Key.Map) == 0 && kv.Value.List.Count == size)
            .ToList();
        if (candidates.Count != 1) return false;
        var match = candidates[0];
        learnedBlocks[match.Key.Map] = block;
        Apply(serverId, match.Value.List, match.Value.At);
        return true;
    }

    private bool IsFieldBlock(int block) =>
        data().FieldBossMaps.Values.Any(m => m.Block == block) || learnedBlocks.ContainsValue(block);

    private int BlockOf(int mapId) =>
        data().FieldBossMaps.TryGetValue(mapId, out var map) ? map.Block : learnedBlocks.GetValueOrDefault(mapId);

    private int RespawnOf(int code) =>
        respawnMinutes.TryGetValue(code, out var learned) ? learned : data().RespawnMinutes.GetValueOrDefault(code);
}
```

`AionDpsMeter.Timers/Bosses/TimerStateStore.cs`:

```csharp
using System.Text.Json;

namespace AionDpsMeter.Timers.Bosses;

/// <summary>timers-state.json next to the exe; written atomically so a crash mid-save leaves the old file.</summary>
public sealed class TimerStateStore(string path)
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public TrackerState? Load()
    {
        try
        {
            return File.Exists(path) ? JsonSerializer.Deserialize<TrackerState>(File.ReadAllText(path), Options) : null;
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    public void Save(TrackerState state)
    {
        try
        {
            var tmp = path + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(state, Options));
            File.Move(tmp, path, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test AionDpsMeter.Timers.Tests`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add AionDpsMeter.Timers AionDpsMeter.Timers.Tests
git commit -m "feat(timers): per-server field boss tracker with persistence"
```

---

### Task 7: Подключение к пакетам и сущностям RATmeter

**Files:**
- Create (новые файлы в проекте апстрима): `AionDpsMeter.Services/PacketProcessing/Fork/IFieldBossListListener.cs`, `AionDpsMeter.Services/PacketProcessing/Processors/FieldBossListForwarder.cs`
- Create: `AionDpsMeter.Timers/Runtime/Abstractions.cs`, `AionDpsMeter.Timers/Runtime/KillWatcher.cs`, `AionDpsMeter.Timers/Runtime/EntityTrackerAdapters.cs`, `AionDpsMeter.Timers/Bosses/FieldBossListListener.cs`
- Test: `AionDpsMeter.Timers.Tests/Runtime/KillWatcherTests.cs`, `AionDpsMeter.Timers.Tests/Bosses/FieldBossListListenerTests.cs`, `AionDpsMeter.Timers.Tests/Runtime/OpcodeOwnershipTests.cs`

**Interfaces:**
- Consumes: `FieldBossTracker`, `FieldBossListParser`, `Wire`, `BossCatalog`; из апстрима — `Packet` (`byte[] Data`, `long ReceivedAt`), `EntityTracker` (`List<Player> PlayerEntities`, `List<Mob> TargetEntities`), `Player.IsUser`, `Player.ServerId`, `Mob.Id`, `Mob.MobCode`, `Mob.HpTotal`, `Mob.HpCurrent`.
- Produces:
  - `AionDpsMeter.Services.PacketProcessing.Fork.IFieldBossListListener { void OnFieldBossList(Packet packet); }`
  - `interface IServerContext { int CurrentServerId { get; } }`, `interface IMobHpSource { IReadOnlyList<MobHp> Snapshot(); }`, `readonly record struct MobHp(int EntityId, int MobCode, long HpTotal, long HpCurrent)`
  - `class KillWatcher(IMobHpSource, IServerContext, BossCatalog, FieldBossTracker)` с `void Tick()`
  - `class EntityTrackerServerContext(EntityTracker) : IServerContext`, `class EntityTrackerHpSource(EntityTracker) : IMobHpSource`
  - `class FieldBossListListener(FieldBossTracker, IServerContext, ILogger<FieldBossListListener>) : IFieldBossListListener`

- [ ] **Step 1: Добавить переходник в проект Services (новые файлы, правок апстрима нет)**

Атрибут `[PacketOpcode]` в апстриме `internal`, а один опкод может обрабатывать только один класс — поэтому обработчик живёт внутри сборки Services и лишь передаёт пакет слушателям.

`AionDpsMeter.Services/PacketProcessing/Fork/IFieldBossListListener.cs`:

```csharp
// aion2-overlay fork: new file (see FORK_CHANGES.md).
using AionDpsMeter.Services.PacketProcessing.Routing;

namespace AionDpsMeter.Services.PacketProcessing.Fork
{
    /// <summary>Receives the in-game map's field boss list packets (wire bytes 01 91).</summary>
    public interface IFieldBossListListener
    {
        void OnFieldBossList(Packet packet);
    }
}
```

`AionDpsMeter.Services/PacketProcessing/Processors/FieldBossListForwarder.cs`:

```csharp
// aion2-overlay fork: new file (see FORK_CHANGES.md).
using AionDpsMeter.Services.PacketProcessing.Fork;
using AionDpsMeter.Services.PacketProcessing.Routing;

namespace AionDpsMeter.Services.PacketProcessing.Processors
{
    /// <summary>Hands the field boss list (wire bytes 01 91) to the overlay's timers. Registered by upstream's
    /// assembly scan like every other processor; with no listener registered it does nothing.</summary>
    [PacketOpcode(0x9101)]
    internal sealed class FieldBossListForwarder(IEnumerable<IFieldBossListListener> listeners) : IOpcodeProcessor
    {
        public void Process(Packet packet)
        {
            foreach (var listener in listeners) listener.OnFieldBossList(packet);
        }
    }
}
```

- [ ] **Step 2: Write the failing tests**

`AionDpsMeter.Timers.Tests/Runtime/KillWatcherTests.cs`:

```csharp
using AionDpsMeter.Timers.Bosses;
using AionDpsMeter.Timers.Runtime;
using AionDpsMeter.Timers.Schedule;
using Microsoft.Extensions.Time.Testing;

namespace AionDpsMeter.Timers.Tests.Runtime;

internal sealed class FakeServer(int id) : IServerContext
{
    public int CurrentServerId => id;
}

public class KillWatcherTests
{
    private sealed class FakeMobs : IMobHpSource
    {
        public List<MobHp> Mobs { get; } = new();
        public bool Throw { get; set; }
        public IReadOnlyList<MobHp> Snapshot() => Throw ? throw new InvalidOperationException("modified") : Mobs.ToList();
    }

    private static readonly BossCatalog Catalog = BossCatalog.FromJson("""
        { "2400800": { "name": "Гартуа", "isBoss": true }, "2000002": { "name": "Моб", "isBoss": false } }
        """);

    private readonly FakeMobs mobs = new();
    private readonly FieldBossTracker tracker = new(Catalog,
        () => new ScheduleData([], new Dictionary<int, FieldBossMapInfo> { [1110] = new(2400, "Альтгард") }, new Dictionary<int, int>()),
        new FakeTimeProvider(new DateTimeOffset(2026, 10, 7, 12, 0, 0, TimeSpan.Zero)));

    private KillWatcher Watcher() => new(mobs, new FakeServer(7), Catalog, tracker);

    [Fact]
    public void A_boss_seen_alive_then_at_zero_hp_is_a_kill()
    {
        var w = Watcher();
        mobs.Mobs.Add(new MobHp(501, 2400800, 1_000, 400));
        w.Tick();
        mobs.Mobs[0] = mobs.Mobs[0] with { HpCurrent = 0 };
        w.Tick();
        w.Tick(); // still dead: no second kill

        var timer = Assert.Single(tracker.TimersFor(7));
        Assert.Equal(2400800, timer.Key);
        Assert.NotNull(timer.LastKill);
    }

    [Fact]
    public void A_corpse_never_seen_alive_is_not_a_kill()
    {
        var w = Watcher();
        mobs.Mobs.Add(new MobHp(501, 2400800, 1_000, 0));
        w.Tick();
        Assert.Empty(tracker.TimersFor(7));
    }

    [Fact]
    public void Ordinary_mobs_are_ignored()
    {
        var w = Watcher();
        mobs.Mobs.Add(new MobHp(9, 2000002, 100, 50));
        w.Tick();
        mobs.Mobs[0] = mobs.Mobs[0] with { HpCurrent = 0 };
        w.Tick();
        Assert.Empty(tracker.TimersFor(7));
    }

    [Fact]
    public void A_failed_snapshot_is_skipped()
    {
        var w = Watcher();
        mobs.Throw = true;
        w.Tick(); // must not throw
    }
}
```

`AionDpsMeter.Timers.Tests/Bosses/FieldBossListListenerTests.cs`:

```csharp
using AionDpsMeter.Services.PacketProcessing.Routing;
using AionDpsMeter.Timers.Bosses;
using AionDpsMeter.Timers.Schedule;
using AionDpsMeter.Timers.Tests.Runtime;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace AionDpsMeter.Timers.Tests.Bosses;

public class FieldBossListListenerTests
{
    /// <summary>RATmeter's frame: varint length prefix, opcode bytes 01 91, body.</summary>
    private static Packet Frame(byte[] body)
    {
        var payload = new List<byte> { 0x01, 0x91 };
        payload.AddRange(body);
        var head = new List<byte>();
        var v = (uint)(payload.Count + 2);
        do
        {
            var b = (byte)(v & 0x7F);
            v >>= 7;
            if (v != 0) b |= 0x80;
            head.Add(b);
        } while (v != 0);
        return new Packet { Data = [.. head, .. payload], ReceivedAt = 0 };
    }

    private static FieldBossTracker Tracker() =>
        new(BossCatalog.Empty, () => ScheduleData.Empty, new FakeTimeProvider(DateTimeOffset.UtcNow));

    [Fact]
    public void A_list_packet_reaches_the_tracker_under_the_current_server()
    {
        var tracker = Tracker();
        var listener = new FieldBossListListener(tracker, new FakeServer(5), NullLogger<FieldBossListListener>.Instance);

        listener.OnFieldBossList(Frame(Fixtures.Bytes(Fixtures.AltgardList)));

        Assert.Equal(24, tracker.TimersFor(5).Count);
    }

    [Fact]
    public void A_garbage_packet_is_dropped_quietly()
    {
        var tracker = Tracker();
        var listener = new FieldBossListListener(tracker, new FakeServer(5), NullLogger<FieldBossListListener>.Instance);

        listener.OnFieldBossList(new Packet { Data = [0xFF, 0xFF], ReceivedAt = 0 });
        listener.OnFieldBossList(Frame([1, 2, 3]));

        Assert.Empty(tracker.TimersFor(5));
    }
}
```

`AionDpsMeter.Timers.Tests/Runtime/OpcodeOwnershipTests.cs`:

```csharp
using AionDpsMeter.Services.PacketProcessing.Fork;
using AionDpsMeter.Timers.Bosses;

namespace AionDpsMeter.Timers.Tests.Runtime;

public class OpcodeOwnershipTests
{
    /// <summary>
    /// Upstream's registry throws at startup when two processors claim one opcode. If upstream ever starts handling
    /// 01 91 itself, this fails in CI before a release is published, instead of the app failing to start.
    /// </summary>
    [Fact]
    public void Only_the_fork_forwarder_handles_the_field_boss_list_opcode()
    {
        var owners = typeof(IFieldBossListListener).Assembly.GetTypes()
            .Where(t => t.CustomAttributes.Any(a =>
                a.AttributeType.Name == "PacketOpcodeAttribute" &&
                a.ConstructorArguments.Count == 1 &&
                Convert.ToUInt16(a.ConstructorArguments[0].Value) == FieldBossListParser.Opcode))
            .Select(t => t.Name)
            .ToList();

        Assert.Equal(["FieldBossListForwarder"], owners);
    }
}
```

- [ ] **Step 3: Run tests to verify they fail**

Run: `dotnet test AionDpsMeter.Timers.Tests`
Expected: FAIL — ошибка компиляции «AionDpsMeter.Timers.Runtime / FieldBossListListener does not exist».

- [ ] **Step 4: Write the implementation**

`AionDpsMeter.Timers/Runtime/Abstractions.cs`:

```csharp
namespace AionDpsMeter.Timers.Runtime;

/// <summary>The server of the character being played; 0 until it is known.</summary>
public interface IServerContext
{
    int CurrentServerId { get; }
}

public readonly record struct MobHp(int EntityId, int MobCode, long HpTotal, long HpCurrent);

public interface IMobHpSource
{
    IReadOnlyList<MobHp> Snapshot();
}
```

`AionDpsMeter.Timers/Runtime/KillWatcher.cs`:

```csharp
using AionDpsMeter.Timers.Bosses;

namespace AionDpsMeter.Timers.Runtime;

/// <summary>
/// Polls the meter's mob table once a second: a boss seen with HP that then drops to 0 is a kill.
/// Upstream's death packet is handled by upstream (one processor per opcode), so this reads the HP it already tracks.
/// </summary>
public sealed class KillWatcher(IMobHpSource source, IServerContext server, BossCatalog catalog, FieldBossTracker tracker)
{
    private readonly HashSet<int> alive = new();

    public void Tick()
    {
        IReadOnlyList<MobHp> mobs;
        try
        {
            mobs = source.Snapshot();
        }
        catch (Exception)
        {
            return; // the packet thread changed the table mid-read: try again next tick
        }

        var present = new HashSet<int>();
        foreach (var m in mobs)
        {
            present.Add(m.EntityId);
            if (m.MobCode == 0 || !catalog.IsBoss(m.MobCode)) continue;
            if (m.HpCurrent > 0) alive.Add(m.EntityId);
            else if (alive.Remove(m.EntityId)) tracker.OnKill(server.CurrentServerId, m.MobCode);
        }
        alive.IntersectWith(present);
    }
}
```

`AionDpsMeter.Timers/Runtime/EntityTrackerAdapters.cs`:

```csharp
using AionDpsMeter.Services.Services.Entity;

namespace AionDpsMeter.Timers.Runtime;

// EntityTracker uses plain dictionaries written by the packet thread; reads from here can race, so they are
// guarded and simply retried on the next tick.

public sealed class EntityTrackerServerContext(EntityTracker entities) : IServerContext
{
    private int last;

    public int CurrentServerId
    {
        get
        {
            try
            {
                var id = entities.PlayerEntities.FirstOrDefault(p => p.IsUser)?.ServerId ?? 0;
                if (id != 0) last = id;
            }
            catch (Exception) { }
            return last;
        }
    }
}

public sealed class EntityTrackerHpSource(EntityTracker entities) : IMobHpSource
{
    public IReadOnlyList<MobHp> Snapshot() =>
        entities.TargetEntities.Select(m => new MobHp(m.Id, m.MobCode, m.HpTotal, m.HpCurrent)).ToList();
}
```

`AionDpsMeter.Timers/Bosses/FieldBossListListener.cs`:

```csharp
using AionDpsMeter.Services.PacketProcessing.Fork;
using AionDpsMeter.Services.PacketProcessing.Routing;
using AionDpsMeter.Timers.Runtime;
using Microsoft.Extensions.Logging;

namespace AionDpsMeter.Timers.Bosses;

public sealed class FieldBossListListener(FieldBossTracker tracker, IServerContext server, ILogger<FieldBossListListener> logger)
    : IFieldBossListListener
{
    public void OnFieldBossList(Packet packet)
    {
        // RATmeter frame: varint length prefix, 2 opcode bytes, body.
        if (!Wire.TryVarint(packet.Data, 0, out _, out var header) || packet.Data.Length < header + 2) return;
        var list = FieldBossListParser.Parse(packet.Data.AsSpan(header + 2));
        if (list is null)
        {
            logger.LogDebug("Field boss list not recognised ({Length} bytes)", packet.Data.Length);
            return;
        }
        if (!list.Complete)
            logger.LogWarning("Field boss list for map {Map}: read {Read} of {Count} slots: {Hex}",
                list.MapId, list.Slots.Count, list.Count, Convert.ToHexString(packet.Data));
        tracker.OnList(server.CurrentServerId, list);
    }
}
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test AionDpsMeter.Timers.Tests`
Expected: PASS.

- [ ] **Step 6: Check that the whole solution still builds**

Run: `dotnet build AionDpsMeter.slnx -c Release`
Expected: `Build succeeded`.

- [ ] **Step 7: Commit**

```bash
git add AionDpsMeter.Services/PacketProcessing AionDpsMeter.Timers AionDpsMeter.Timers.Tests
git commit -m "feat(timers): receive boss list packets and watch boss HP for kills"
```

---

### Task 8: Лента событий, оповещения, настройки и запуск модуля

**Files:**
- Create: `AionDpsMeter.Timers/Feed/FeedItem.cs`, `AionDpsMeter.Timers/Feed/TimersFeed.cs`, `AionDpsMeter.Timers/Feed/FeedText.cs`, `AionDpsMeter.Timers/Feed/AlertService.cs`, `AionDpsMeter.Timers/Feed/TimersOptions.cs`
- Create: `AionDpsMeter.Timers/Runtime/TimersHostedService.cs`, `AionDpsMeter.Timers/TimersServiceCollectionExtensions.cs`
- Test: `AionDpsMeter.Timers.Tests/Feed/TimersFeedTests.cs`, `AionDpsMeter.Timers.Tests/Feed/AlertServiceTests.cs`, `AionDpsMeter.Timers.Tests/Feed/TimersOptionsTests.cs`

**Interfaces:**
- Consumes: всё из Task 2–7.
- Produces:
  - `enum FeedKind { Rift, Event, Boss }`, `enum FeedStatus { Active, Alive, Upcoming, Overdue, Unknown }`
  - `record FeedItem(string Id, string Key, FeedKind Kind, string Title, string? Zone, FeedStatus Status, DateTimeOffset? At, bool Verified, int? BossKey)`
  - `static IReadOnlyList<FeedItem> TimersFeed.Build(DateTimeOffset now, ScheduleData data, IEnumerable<BossTimer> bosses, BossCatalog catalog)`
  - `static string FeedText.Format(FeedItem item, DateTimeOffset now, TimeZoneInfo local)`, `static string FeedText.Span(TimeSpan t)`
  - `class AlertService` с `IReadOnlyList<FeedItem> Due(IEnumerable<FeedItem> items, DateTimeOffset now, Func<FeedKind, int> leadMinutes)`
  - `class TimersOptions` (`BossLeadMinutes`, `RiftLeadMinutes`, `EventLeadMinutes`, `Sound`, `HiddenKinds`, `HiddenIds`, `int LeadFor(FeedKind)`, `bool Shows(FeedItem)`), `class TimersOptionsStore(string path)` (`Current`, `Load()`, `Save()`)
  - `static IServiceCollection AddTimers(this IServiceCollection services)`; `static string TimersPaths.Of(string fileName)`

- [ ] **Step 1: Добавить пакет хостинга**

```bash
dotnet add AionDpsMeter.Timers package Microsoft.Extensions.Hosting.Abstractions
```

- [ ] **Step 2: Write the failing tests**

`AionDpsMeter.Timers.Tests/Feed/TimersFeedTests.cs`:

```csharp
using AionDpsMeter.Timers.Bosses;
using AionDpsMeter.Timers.Feed;
using AionDpsMeter.Timers.Schedule;

namespace AionDpsMeter.Timers.Tests.Feed;

public class TimersFeedTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 7, 10, 30, 0, TimeSpan.Zero);
    private static readonly BossCatalog Catalog = BossCatalog.FromJson("""{ "2400800": { "name": "Гартуа", "isBoss": true } }""");

    private static ScheduleData Data(params ScheduledEvent[] events) =>
        new(events, new Dictionary<int, FieldBossMapInfo> { [1110] = new(2400, "Альтгард") }, new Dictionary<int, int>());

    private static ScheduledEvent Rift(bool verified = false, int duration = 0) =>
        new("rift", "Разлом", "rift", ScheduleKind.Fixed, TimeZoneInfo.Utc, [new TimeOnly(11, 0)], null, null, null,
            TimeSpan.FromMinutes(duration), verified);

    private static BossTimer Boss(int key, bool alive = false, DateTimeOffset? next = null, DateTimeOffset? since = null) =>
        new(1, key, 1110, 111021, alive, since, next, null, null);

    [Fact]
    public void Schedule_events_become_upcoming_items_marked_unverified()
    {
        var item = Assert.Single(TimersFeed.Build(Now, Data(Rift()), [], Catalog));
        Assert.Equal(FeedKind.Rift, item.Kind);
        Assert.Equal(FeedStatus.Upcoming, item.Status);
        Assert.Equal(Now.AddMinutes(30), item.At);
        Assert.False(item.Verified);
        Assert.Equal("rift", item.Id);
    }

    [Fact]
    public void A_running_event_is_active_until_its_end()
    {
        var item = Assert.Single(TimersFeed.Build(Now.AddMinutes(40), Data(Rift(duration: 60)), [], Catalog));
        Assert.Equal(FeedStatus.Active, item.Status);
        Assert.Equal(Now.AddMinutes(90), item.At);
    }

    [Fact]
    public void Boss_statuses_follow_the_timer()
    {
        var items = TimersFeed.Build(Now, Data(), [
            Boss(2400800, next: Now.AddMinutes(20)),
            Boss(-111003, alive: true, since: Now.AddMinutes(-3)),
            Boss(-111004, next: Now.AddMinutes(-10)),
            Boss(-111005, next: Now.AddHours(-3)),
        ], Catalog);

        Assert.Equal(FeedStatus.Upcoming, items.Single(i => i.BossKey == 2400800).Status);
        Assert.Equal("Гартуа", items.Single(i => i.BossKey == 2400800).Title);
        Assert.Equal("Альтгард", items.Single(i => i.BossKey == 2400800).Zone);
        Assert.Equal(FeedStatus.Alive, items.Single(i => i.BossKey == -111003).Status);
        Assert.Equal(FeedStatus.Overdue, items.Single(i => i.BossKey == -111004).Status);
        Assert.Equal(FeedStatus.Unknown, items.Single(i => i.BossKey == -111005).Status);
        Assert.Equal("Босс №21", TimersFeed.Build(Now, Data(), [Boss(-111021)], Catalog).Single().Title);
    }

    [Fact]
    public void Items_are_ordered_running_first_then_soonest()
    {
        var items = TimersFeed.Build(Now, Data(Rift()), [
            Boss(-111001, next: Now.AddMinutes(5)),
            Boss(-111002, alive: true, since: Now.AddMinutes(-1)),
            Boss(-111003, next: Now.AddHours(-5)),
        ], Catalog);
        Assert.Equal([-111002, -111001, null, -111003], items.Select(i => i.BossKey));
    }

    [Fact]
    public void Text_formats()
    {
        var local = TimeZoneInfo.CreateCustomTimeZone("UTC+3", TimeSpan.FromHours(3), "UTC+3", "UTC+3");
        Assert.Equal("1:02:03", FeedText.Span(new TimeSpan(1, 2, 3)));
        Assert.Equal("1:05", FeedText.Span(TimeSpan.FromSeconds(65)));
        Assert.Equal("0:00", FeedText.Span(TimeSpan.FromSeconds(-5)));
        var alive = new FeedItem("b", "b", FeedKind.Boss, "B", null, FeedStatus.Alive, Now, true, 1);
        Assert.Equal("жив с 13:30", FeedText.Format(alive, Now, local));
        var soon = alive with { Status = FeedStatus.Upcoming, At = Now.AddMinutes(90) };
        Assert.Equal("через 1:30:00", FeedText.Format(soon, Now, local));
        Assert.Equal("?", FeedText.Format(alive with { Status = FeedStatus.Unknown, At = null }, Now, local));
    }
}
```

`AionDpsMeter.Timers.Tests/Feed/AlertServiceTests.cs`:

```csharp
using AionDpsMeter.Timers.Feed;

namespace AionDpsMeter.Timers.Tests.Feed;

public class AlertServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 7, 10, 0, 0, TimeSpan.Zero);

    private static FeedItem Item(string id, FeedKind kind, DateTimeOffset at, FeedStatus status = FeedStatus.Upcoming) =>
        new(id, $"{id}@{at:O}", kind, id, null, status, at, true, null);

    private static int Lead(FeedKind kind) => kind == FeedKind.Boss ? 5 : 2;

    [Fact]
    public void Alerts_once_inside_the_lead_time()
    {
        var alerts = new AlertService();
        var boss = Item("boss", FeedKind.Boss, Now.AddMinutes(4));
        var rift = Item("rift", FeedKind.Rift, Now.AddMinutes(4)); // rift lead is 2 min: not yet

        Assert.Equal([boss], alerts.Due([boss, rift], Now, Lead));
        Assert.Empty(alerts.Due([boss, rift], Now.AddSeconds(1), Lead));
        Assert.Equal([rift], alerts.Due([boss, rift], Now.AddMinutes(2), Lead));
    }

    [Fact]
    public void A_new_spawn_time_alerts_again()
    {
        var alerts = new AlertService();
        Assert.Single(alerts.Due([Item("boss", FeedKind.Boss, Now.AddMinutes(3))], Now, Lead));
        Assert.Single(alerts.Due([Item("boss", FeedKind.Boss, Now.AddMinutes(4))], Now, Lead));
    }

    [Fact]
    public void Running_alive_and_past_items_never_alert()
    {
        var alerts = new AlertService();
        Assert.Empty(alerts.Due([
            Item("a", FeedKind.Boss, Now.AddMinutes(1), FeedStatus.Alive),
            Item("b", FeedKind.Rift, Now.AddMinutes(1), FeedStatus.Active),
            Item("c", FeedKind.Boss, Now.AddMinutes(-1), FeedStatus.Overdue),
        ], Now, Lead));
    }
}
```

`AionDpsMeter.Timers.Tests/Feed/TimersOptionsTests.cs`:

```csharp
using AionDpsMeter.Timers.Feed;

namespace AionDpsMeter.Timers.Tests.Feed;

public class TimersOptionsTests
{
    [Fact]
    public void Options_round_trip_and_filter()
    {
        using var dir = new TempDir();
        var store = new TimersOptionsStore(dir.File("timers-settings.json"));
        store.Load();
        Assert.Equal(5, store.Current.LeadFor(FeedKind.Boss));
        store.Current.HiddenKinds.Add(FeedKind.Event);
        store.Current.HiddenIds.Add("boss:2400800");
        store.Current.RiftLeadMinutes = 3;
        store.Save();

        var again = new TimersOptionsStore(dir.File("timers-settings.json"));
        again.Load();
        Assert.Equal(3, again.Current.LeadFor(FeedKind.Rift));
        var item = new FeedItem("boss:2400800", "k", FeedKind.Boss, "B", null, FeedStatus.Upcoming, null, true, 2400800);
        Assert.False(again.Current.Shows(item));
        Assert.False(again.Current.Shows(item with { Id = "x", Kind = FeedKind.Event }));
        Assert.True(again.Current.Shows(item with { Id = "x" }));
    }

    [Fact]
    public void A_corrupt_settings_file_gives_defaults()
    {
        using var dir = new TempDir();
        File.WriteAllText(dir.File("timers-settings.json"), "{ nope");
        var store = new TimersOptionsStore(dir.File("timers-settings.json"));
        store.Load();
        Assert.Equal(2, store.Current.RiftLeadMinutes);
    }
}
```

- [ ] **Step 3: Run tests to verify they fail**

Run: `dotnet test AionDpsMeter.Timers.Tests`
Expected: FAIL — ошибка компиляции «AionDpsMeter.Timers.Feed does not exist».

- [ ] **Step 4: Write the implementation**

`AionDpsMeter.Timers/Feed/FeedItem.cs`:

```csharp
namespace AionDpsMeter.Timers.Feed;

public enum FeedKind { Rift, Event, Boss }

public enum FeedStatus { Active, Alive, Upcoming, Overdue, Unknown }

/// <param name="Id">Stable across occurrences (used to hide an item): the schedule event id, or "boss:{key}".</param>
/// <param name="Key">One occurrence (used to alert once): Id + time.</param>
/// <param name="At">Active: ends at. Alive: alive since. Upcoming / Overdue: starts or spawns at.</param>
/// <param name="BossKey">The tracker key for bosses (for the "убит" button); null for schedule events.</param>
public sealed record FeedItem(
    string Id,
    string Key,
    FeedKind Kind,
    string Title,
    string? Zone,
    FeedStatus Status,
    DateTimeOffset? At,
    bool Verified,
    int? BossKey);
```

`AionDpsMeter.Timers/Feed/TimersFeed.cs`:

```csharp
using AionDpsMeter.Timers.Bosses;
using AionDpsMeter.Timers.Schedule;

namespace AionDpsMeter.Timers.Feed;

public static class TimersFeed
{
    /// <summary>How long a boss stays "должен быть" after its spawn time before it turns into "?".</summary>
    public static readonly TimeSpan OverdueWindow = TimeSpan.FromMinutes(60);

    public static IReadOnlyList<FeedItem> Build(DateTimeOffset now, ScheduleData data, IEnumerable<BossTimer> bosses, BossCatalog catalog)
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

        foreach (var b in bosses)
        {
            var title = b.Key > 0 ? catalog.Name(b.Key) : $"Босс №{b.SlotId % 100}";
            var zone = data.FieldBossMaps.TryGetValue(b.MapId, out var map) ? map.Name : null;
            var (status, at) = b switch
            {
                { Alive: true } => (FeedStatus.Alive, b.AliveSince),
                { NextSpawn: { } next } when next > now => (FeedStatus.Upcoming, (DateTimeOffset?)next),
                { NextSpawn: { } next } when now - next < OverdueWindow => (FeedStatus.Overdue, (DateTimeOffset?)next),
                _ => (FeedStatus.Unknown, (DateTimeOffset?)null),
            };
            items.Add(new FeedItem($"boss:{b.Key}", $"boss:{b.Key}@{at:O}", FeedKind.Boss, title, zone, status, at, true, b.Key));
        }

        return items
            .OrderBy(i => Rank(i.Status))
            .ThenBy(i => i.At ?? DateTimeOffset.MaxValue)
            .ThenBy(i => i.Title, StringComparer.CurrentCulture)
            .ToList();
    }

    private static int Rank(FeedStatus status) => status switch
    {
        FeedStatus.Active or FeedStatus.Alive => 0,
        FeedStatus.Upcoming => 1,
        FeedStatus.Overdue => 2,
        _ => 3,
    };
}
```

`AionDpsMeter.Timers/Feed/FeedText.cs`:

```csharp
namespace AionDpsMeter.Timers.Feed;

public static class FeedText
{
    public static string Format(FeedItem item, DateTimeOffset now, TimeZoneInfo local) => item.Status switch
    {
        FeedStatus.Active when item.At is { } end => $"идёт, ещё {Span(end - now)}",
        FeedStatus.Alive => item.At is { } since ? $"жив с {TimeZoneInfo.ConvertTime(since, local):HH:mm}" : "жив",
        FeedStatus.Upcoming when item.At is { } at => $"через {Span(at - now)}",
        FeedStatus.Overdue when item.At is { } due => $"должен быть ({Span(now - due)} назад)",
        _ => "?",
    };

    public static string Span(TimeSpan t)
    {
        if (t < TimeSpan.Zero) t = TimeSpan.Zero;
        return t.TotalHours >= 1
            ? $"{(int)t.TotalHours}:{t.Minutes:00}:{t.Seconds:00}"
            : $"{t.Minutes}:{t.Seconds:00}";
    }
}
```

`AionDpsMeter.Timers/Feed/AlertService.cs`:

```csharp
namespace AionDpsMeter.Timers.Feed;

public sealed class AlertService
{
    private readonly HashSet<string> fired = new();

    /// <summary>Upcoming items starting within their lead time. Each occurrence (Key) is returned once.</summary>
    public IReadOnlyList<FeedItem> Due(IEnumerable<FeedItem> items, DateTimeOffset now, Func<FeedKind, int> leadMinutes)
    {
        var due = new List<FeedItem>();
        foreach (var item in items)
        {
            if (item.Status != FeedStatus.Upcoming || item.At is not { } at) continue;
            var lead = leadMinutes(item.Kind);
            if (lead <= 0 || at - now > TimeSpan.FromMinutes(lead)) continue;
            if (fired.Add(item.Key)) due.Add(item);
        }
        return due;
    }
}
```

`AionDpsMeter.Timers/Feed/TimersOptions.cs`:

```csharp
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AionDpsMeter.Timers.Feed;

public sealed class TimersOptions
{
    public int BossLeadMinutes { get; set; } = 5;
    public int RiftLeadMinutes { get; set; } = 2;
    public int EventLeadMinutes { get; set; } = 5;
    public bool Sound { get; set; } = true;
    public HashSet<FeedKind> HiddenKinds { get; set; } = [];
    public HashSet<string> HiddenIds { get; set; } = [];

    public int LeadFor(FeedKind kind) => kind switch
    {
        FeedKind.Boss => BossLeadMinutes,
        FeedKind.Rift => RiftLeadMinutes,
        _ => EventLeadMinutes,
    };

    public bool Shows(FeedItem item) => !HiddenKinds.Contains(item.Kind) && !HiddenIds.Contains(item.Id);
}

/// <summary>timers-settings.json next to the exe; edit it by hand to change lead times or sound.</summary>
public sealed class TimersOptionsStore(string path)
{
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public TimersOptions Current { get; private set; } = new();

    public void Load()
    {
        try
        {
            Current = File.Exists(path) ? JsonSerializer.Deserialize<TimersOptions>(File.ReadAllText(path), Json) ?? new() : new();
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            Current = new();
        }
    }

    public void Save()
    {
        try
        {
            File.WriteAllText(path, JsonSerializer.Serialize(Current, Json));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }
}
```

`AionDpsMeter.Timers/Runtime/TimersHostedService.cs`:

```csharp
using AionDpsMeter.Timers.Bosses;
using AionDpsMeter.Timers.Schedule;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AionDpsMeter.Timers.Runtime;

public static class TimersPaths
{
    public static string Of(string fileName) => Path.Combine(AppContext.BaseDirectory, fileName);
}

/// <summary>Loads saved state and the schedule, then every second watches for kills, saves when something changed
/// (at most every 5 s) and refreshes schedule.json from GitHub every 6 hours.</summary>
public sealed class TimersHostedService(
    ScheduleSource schedule,
    FieldBossTracker tracker,
    KillWatcher kills,
    TimerStateStore store,
    TimeProvider time,
    ILogger<TimersHostedService> logger) : BackgroundService
{
    private static readonly TimeSpan SaveEvery = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan RefreshEvery = TimeSpan.FromHours(6);

    private volatile bool dirty;

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        try
        {
            schedule.LoadLocal();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Built-in schedule could not be read");
        }
        tracker.Import(store.Load());
        tracker.Changed += () => dirty = true;
        dirty = false;
        _ = RefreshAsync(ct);

        var lastSave = time.GetUtcNow();
        var lastRefresh = lastSave;
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1), time);
        try
        {
            while (await timer.WaitForNextTickAsync(ct))
            {
                kills.Tick();
                var now = time.GetUtcNow();
                if (dirty && now - lastSave >= SaveEvery)
                {
                    dirty = false;
                    lastSave = now;
                    store.Save(tracker.Export());
                }
                if (now - lastRefresh >= RefreshEvery)
                {
                    lastRefresh = now;
                    _ = RefreshAsync(ct);
                }
            }
        }
        catch (OperationCanceledException) { }
        finally
        {
            store.Save(tracker.Export());
        }
    }

    private async Task RefreshAsync(CancellationToken ct)
    {
        var ok = await schedule.RefreshAsync(ct);
        logger.LogInformation("Schedule refresh {Result}; using {Origin}", ok ? "succeeded" : "failed", schedule.Origin);
    }
}
```

`AionDpsMeter.Timers/TimersServiceCollectionExtensions.cs`:

```csharp
using AionDpsMeter.Services.PacketProcessing.Fork;
using AionDpsMeter.Timers.Bosses;
using AionDpsMeter.Timers.Feed;
using AionDpsMeter.Timers.Runtime;
using AionDpsMeter.Timers.Schedule;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AionDpsMeter.Timers;

public static class TimersServiceCollectionExtensions
{
    public static IServiceCollection AddTimers(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton(_ => BossCatalog.LoadDefault());
        services.AddSingleton(_ => new ScheduleSource(
            new HttpClient { Timeout = TimeSpan.FromSeconds(15) },
            new Uri(ScheduleSource.DefaultRemote),
            TimersPaths.Of("schedule-cache.json"),
            ScheduleSource.ReadEmbedded));
        services.AddSingleton(sp => new FieldBossTracker(
            sp.GetRequiredService<BossCatalog>(),
            () => sp.GetRequiredService<ScheduleSource>().Current,
            sp.GetRequiredService<TimeProvider>()));
        services.AddSingleton(_ => new TimerStateStore(TimersPaths.Of("timers-state.json")));
        services.AddSingleton(_ =>
        {
            var store = new TimersOptionsStore(TimersPaths.Of("timers-settings.json"));
            store.Load();
            store.Save(); // writes the defaults once so the file is there to edit
            return store;
        });
        services.AddSingleton<IServerContext, EntityTrackerServerContext>();
        services.AddSingleton<IMobHpSource, EntityTrackerHpSource>();
        services.AddSingleton<KillWatcher>();
        services.AddSingleton<AlertService>();
        services.AddSingleton<IFieldBossListListener, FieldBossListListener>();
        services.AddHostedService<TimersHostedService>();
        return services;
    }
}
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test AionDpsMeter.Timers.Tests`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add AionDpsMeter.Timers AionDpsMeter.Timers.Tests
git commit -m "feat(timers): feed, alerts, options and service registration"
```

---

### Task 9: Окно таймеров поверх игры

**Files:**
- Create: `AionDpsMeter.UI/Services/TimerWindow/TimersWindowController.cs`, `AionDpsMeter.UI/Services/TimerWindow/TimersOverlayServices.cs`
- Create: `AionDpsMeter.UI/Pages/TimersOverlay.razor`, `AionDpsMeter.UI/Pages/TimersOverlay.razor.cs`, `AionDpsMeter.UI/Pages/TimersOverlay.razor.css`
- Modify (апстрим): `AionDpsMeter.UI/App.xaml.cs` — 2 строки

**Interfaces:**
- Consumes: `AddTimers()`, `FieldBossTracker`, `ScheduleSource`, `BossCatalog`, `IServerContext`, `AlertService`, `TimersOptionsStore`, `TimersFeed`, `FeedText`; из апстрима — `IWindowManagerService` (`Open`, `SetClickThrough`, `RestoreClickThrough`, `Drag`), `BlazorWindow(IServiceProvider, Type)`, `GlobalHotkey(Window)` (`Register(uint modifiers, uint vk, int id)`, `event Action? HotkeyPressed`), `WindowKey`, `WindowPersistenceMode.OnlyPosition`, `App.AppHost`.
- Produces: `class TimersWindowController` (`static readonly WindowKey Key`, `bool IsEditing`, `event Action? EditingChanged`, `void Open()`, `void ToggleEditing()`, `void Drag()`), `static IServiceCollection AddTimersOverlay(this IServiceCollection services)`.

- [ ] **Step 1: Контроллер окна и регистрация**

`AionDpsMeter.UI/Services/TimerWindow/TimersWindowController.cs`:

```csharp
// aion2-overlay fork: new file (see FORK_CHANGES.md).
using AionDpsMeter.Core.Windowing;
using AionDpsMeter.UI.Pages;
using AionDpsMeter.UI.Services.Windowing;
using AionDpsMeter.UI.Utils;

namespace AionDpsMeter.UI.Services.TimerWindow
{
    /// <summary>The timers overlay: click-through over the game; Ctrl+Shift+T toggles edit mode (drag, buttons, filters).</summary>
    public sealed class TimersWindowController(IWindowManagerService windowManager)
    {
        /// <summary>Deliberately not a member of upstream's WindowKey enum (no edit to their file): the window manager
        /// keys windows by value and saves bounds under key.ToString(), i.e. "1001".</summary>
        public static readonly WindowKey Key = (WindowKey)1001;

        private const uint ModControl = 0x0002, ModShift = 0x0004, ModNoRepeat = 0x4000, VkT = 0x54;
        private const int HotkeyId = 9101;

        private GlobalHotkey? hotkey;

        public bool IsEditing { get; private set; }

        public event Action? EditingChanged;

        public void Open()
        {
            var window = new BlazorWindow(App.AppHost.Services, typeof(TimersOverlay))
            {
                Width = 340,
                Height = 460,
                ShowInTaskbar = false,
                Title = "AION2 Timers",
            };
            windowManager.Open(Key, window, isSingleton: true, persistenceMode: WindowPersistenceMode.OnlyPosition);
            windowManager.SetClickThrough(Key);

            hotkey = new GlobalHotkey(window);
            hotkey.Register(ModControl | ModShift | ModNoRepeat, VkT, HotkeyId);
            hotkey.HotkeyPressed += ToggleEditing;
        }

        public void ToggleEditing()
        {
            IsEditing = !IsEditing;
            if (IsEditing) windowManager.RestoreClickThrough(Key);
            else windowManager.SetClickThrough(Key);
            EditingChanged?.Invoke();
        }

        public void Drag() => windowManager.Drag(Key);
    }
}
```

`AionDpsMeter.UI/Services/TimerWindow/TimersOverlayServices.cs`:

```csharp
// aion2-overlay fork: new file (see FORK_CHANGES.md).
using AionDpsMeter.Timers;
using Microsoft.Extensions.DependencyInjection;

namespace AionDpsMeter.UI.Services.TimerWindow
{
    public static class TimersOverlayServices
    {
        public static IServiceCollection AddTimersOverlay(this IServiceCollection services)
        {
            services.AddTimers();
            services.AddSingleton<TimersWindowController>();
            return services;
        }
    }
}
```

- [ ] **Step 2: Компонент оверлея**

`AionDpsMeter.UI/Pages/TimersOverlay.razor`:

```razor
@* aion2-overlay fork: new file (see FORK_CHANGES.md). *@
@using AionDpsMeter.Timers.Feed
@implements IDisposable

<div class="timers @(IsEditing ? "timers--editing" : "")">
    @if (IsEditing)
    {
        <div class="timers__bar" @onmousedown="Drag">Таймеры — тащи за эту полосу · Ctrl+Shift+T закрепить</div>
        <div class="timers__filters">
            @foreach (var kind in Enum.GetValues<FeedKind>())
            {
                <label>
                    <input type="checkbox" checked="@(!Options.HiddenKinds.Contains(kind))"
                           @onchange="e => ToggleKind(kind, e.Value is true)" />
                    @KindLabel(kind)
                </label>
            }
        </div>
    }
    @if (toast is not null)
    {
        <div class="timers__toast">@toast</div>
    }
    @foreach (var item in visible)
    {
        <div class="timers__row timers__row--@item.Status.ToString().ToLowerInvariant()">
            <span class="timers__icon">@KindIcon(item.Kind)</span>
            <span class="timers__title">
                @item.Title
                @if (!item.Verified)
                {
                    <em> (не проверено)</em>
                }
                @if (item.Zone is not null)
                {
                    <small>@item.Zone</small>
                }
            </span>
            <span class="timers__time">@FeedText.Format(item, now, TimeZoneInfo.Local)</span>
            @if (IsEditing)
            {
                if (item.Kind == FeedKind.Boss)
                {
                    <button @onclick="() => MarkKilled(item)">убит</button>
                }
                <button title="Скрыть" @onclick="() => Hide(item)">×</button>
            }
        </div>
    }
    @if (visible.Count == 0)
    {
        <div class="timers__empty">Пока пусто. Открой карту в игре — появятся полевые боссы.</div>
    }
    @if (IsEditing && Options.HiddenIds.Count > 0)
    {
        <button class="timers__showall" @onclick="ShowAll">Показать скрытые (@Options.HiddenIds.Count)</button>
    }
</div>
```

`AionDpsMeter.UI/Pages/TimersOverlay.razor.cs`:

```csharp
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
```

`AionDpsMeter.UI/Pages/TimersOverlay.razor.css`:

```css
/* aion2-overlay fork: new file (see FORK_CHANGES.md). */
.timers { font-size: 13px; color: #e6edf3; padding: 6px; user-select: none; box-sizing: border-box; }
.timers--editing { background: rgba(0, 0, 0, .55); border: 1px dashed rgba(255, 255, 255, .5); border-radius: 6px; }
.timers__bar { cursor: move; padding: 4px 6px; margin-bottom: 4px; background: rgba(255, 255, 255, .08); border-radius: 4px; font-size: 11px; }
.timers__filters { display: flex; gap: 10px; margin-bottom: 6px; font-size: 12px; }
.timers__toast { background: rgba(255, 170, 0, .9); color: #111; padding: 4px 6px; border-radius: 4px; margin-bottom: 4px; font-weight: 700; }
.timers__row { display: flex; align-items: center; gap: 6px; padding: 3px 6px; margin-bottom: 2px; background: rgba(0, 0, 0, .45); border-radius: 4px; text-shadow: 0 0 2px #000; }
.timers__icon { width: 16px; text-align: center; }
.timers__title { flex: 1; min-width: 0; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.timers__title small { opacity: .6; margin-left: 6px; }
.timers__title em { opacity: .6; font-size: 11px; font-style: normal; }
.timers__time { font-variant-numeric: tabular-nums; white-space: nowrap; }
.timers__row--active .timers__time, .timers__row--alive .timers__time { color: #7ee787; }
.timers__row--overdue .timers__time { color: #ffa657; }
.timers__row--unknown { opacity: .55; }
.timers button { background: rgba(255, 255, 255, .12); color: inherit; border: 0; border-radius: 3px; padding: 1px 6px; cursor: pointer; }
.timers__empty { opacity: .7; padding: 6px; font-size: 12px; }
```

- [ ] **Step 3: Подключить в `App.xaml.cs` (2 строки, полные имена — чтобы не трогать `using` апстрима)**

В `ConfigureServices`, сразу после `services.AddWpfBlazorWebView();`:

```csharp
                    AionDpsMeter.UI.Services.TimerWindow.TimersOverlayServices.AddTimersOverlay(services); // aion2-overlay fork
```

В `OnStartup`, сразу после `windowHelper.OpenRequiredWindows();`:

```csharp
            AppHost.Services.GetRequiredService<AionDpsMeter.UI.Services.TimerWindow.TimersWindowController>().Open(); // aion2-overlay fork
```

- [ ] **Step 4: Собрать и прогнать тесты**

Run: `dotnet build AionDpsMeter.slnx -c Release && dotnet test AionDpsMeter.Timers.Tests`
Expected: `Build succeeded`, тесты PASS.

- [ ] **Step 5: Запустить приложение и проверить без игры**

```bash
dotnet run --project AionDpsMeter.UI -c Release
```
Проверить (скриншотом или глазами пользователя):
- рядом с ДПС-окном появилось окно таймеров со строкой «Разлом (не проверено) через …»;
- окно пропускает клики насквозь; `Ctrl+Shift+T` включает рамку, полосу для перетаскивания и чекбоксы, повторное нажатие — закрепляет;
- после перетаскивания и перезапуска окно открывается на том же месте (в `appsettings.user.json` появился ключ `"1001"`);
- рядом с exe (`AionDpsMeter.UI/bin/Release/net10.0-windows*/`) появились `timers-settings.json` и `schedule-cache.json` (последний — если есть доступ к GitHub после Task 10; до публикации `schedule.json` в форке будет 404 и это нормально: используется встроенная копия);
- в логе Serilog нет исключений от `AionDpsMeter.Timers`.

Если окно не появилось — посмотреть лог, затем проверить, что `WindowManagerService.Open` показывает окно (он вызывает `window.Show()`), и что `GlobalHotkey.Register` вызывается после показа окна.

- [ ] **Step 6: Commit**

```bash
git add AionDpsMeter.UI
git commit -m "feat(ui): timers overlay window with click-through and edit mode"
```

---

### Task 10: Автообновление, документация, публикация

**Files:**
- Modify (апстрим): `AionDpsMeter.Services/Services/Update/UpdateCheckerService.cs` — 1 строка
- Create: `.github/workflows/sync-upstream.yml`
- Create: `FORK_CHANGES.md`, `docs/USAGE.ru.md`

**Interfaces:**
- Consumes: всё предыдущее.
- Produces: релизы `vГГГГ.ММДД.N` с файлом `Aion2Overlay-vГГГГ.ММДД.N-win-x64.zip` в `drixed/aion2-overlay`.

- [ ] **Step 1: Направить обновлятор на релизы форка**

В `AionDpsMeter.Services/Services/Update/UpdateCheckerService.cs` заменить строку с `ReleasesApiUrl` на:

```csharp
        private const string ReleasesApiUrl = "https://api.github.com/repos/drixed/aion2-overlay/releases/latest"; // aion2-overlay fork
```

- [ ] **Step 2: Workflow синхронизации и релиза**

`.github/workflows/sync-upstream.yml`:

```yaml
name: Sync upstream and release

on:
  schedule:
    - cron: '17 5 * * *'
  workflow_dispatch:
  push:
    branches: [master]
    paths-ignore: ['docs/**', '**/*.md', 'schedule.json']

permissions:
  contents: write
  issues: write

concurrency:
  group: sync-upstream
  cancel-in-progress: false

jobs:
  sync:
    runs-on: windows-latest
    env:
      # SYNC_TOKEN (personal token with "repo" + "workflow") is only needed when upstream changes its own
      # .github/workflows files: the default token is not allowed to push those. See docs/USAGE.ru.md.
      GH_TOKEN: ${{ secrets.SYNC_TOKEN || github.token }}
    steps:
      - uses: actions/checkout@v4
        with:
          fetch-depth: 0
          token: ${{ secrets.SYNC_TOKEN || github.token }}

      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'

      - name: Merge upstream
        id: merge
        shell: pwsh
        run: |
          git config user.name "github-actions[bot]"
          git config user.email "41898282+github-actions[bot]@users.noreply.github.com"
          git remote add upstream https://github.com/Kuroukihime/AIon2-Dps-Meter.git
          git fetch upstream master
          $before = git rev-parse HEAD
          git merge --no-edit upstream/master
          if ($LASTEXITCODE -ne 0) {
            git diff --name-only --diff-filter=U | Out-File conflicts.txt
            git merge --abort
            exit 1
          }
          $after = git rev-parse HEAD
          "merged=$($after -ne $before)" >> $env:GITHUB_OUTPUT

      - name: Build and test
        shell: pwsh
        run: |
          dotnet build AionDpsMeter.slnx -c Release
          if ($LASTEXITCODE -ne 0) { exit 1 }
          dotnet test AionDpsMeter.Timers.Tests/AionDpsMeter.Timers.Tests.csproj -c Release --no-build
          if ($LASTEXITCODE -ne 0) { exit 1 }

      - name: Push merge
        if: steps.merge.outputs.merged == 'True'
        shell: pwsh
        run: git push origin HEAD:master

      - name: Decide release
        id: decide
        shell: pwsh
        run: |
          $last = gh release view --json tagName -q .tagName 2>$null
          if ($LASTEXITCODE -ne 0 -or -not $last) {
            "release=true" >> $env:GITHUB_OUTPUT
            exit 0
          }
          "last=$last" >> $env:GITHUB_OUTPUT
          git diff --quiet $last HEAD -- . ':(exclude)docs' ':(exclude)*.md' ':(exclude)schedule.json'
          if ($LASTEXITCODE -ne 0) { "release=true" >> $env:GITHUB_OUTPUT } else { "release=false" >> $env:GITHUB_OUTPUT }
          exit 0

      - name: Publish
        id: publish
        if: steps.decide.outputs.release == 'true'
        shell: pwsh
        run: |
          $version = "{0}.{1}.{2}" -f (Get-Date).Year, [int](Get-Date -Format 'MMdd'), $env:GITHUB_RUN_NUMBER
          dotnet publish AionDpsMeter.UI/AionDpsMeter.UI.csproj -c Release -r win-x64 --self-contained true `
            -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
            -p:Version=$version -p:AssemblyVersion=$version -p:FileVersion=$version `
            --output ./publish
          if ($LASTEXITCODE -ne 0) { exit 1 }
          Compress-Archive -Path ./publish/* -DestinationPath "Aion2Overlay-v$version-win-x64.zip"
          "version=$version" >> $env:GITHUB_OUTPUT

      - name: Release
        if: steps.decide.outputs.release == 'true'
        shell: pwsh
        run: |
          $v = "${{ steps.publish.outputs.version }}"
          $last = "${{ steps.decide.outputs.last }}"
          if ($last) {
            $log = (git log --no-merges --format='- %s' "$last..HEAD") -join "`n"
            $notes = "Изменения с ${last}:`n$log"
          } else {
            $notes = "Первый выпуск."
          }
          gh release create "v$v" "Aion2Overlay-v$v-win-x64.zip" --target (git rev-parse HEAD) --title "v$v" --notes $notes

      - name: Report failure
        if: failure()
        shell: pwsh
        run: |
          $body = "Запуск: ${{ github.server_url }}/${{ github.repository }}/actions/runs/${{ github.run_id }}"
          if (Test-Path conflicts.txt) {
            $body += "`n`nКонфликт с апстримом в файлах:`n" + (Get-Content conflicts.txt -Raw)
          }
          gh label create sync-failure --color B60205 --force | Out-Null
          $open = gh issue list --state open --label sync-failure --json number -q '.[0].number'
          if ($open) { gh issue comment $open --body $body }
          else { gh issue create --title "Автообновление сломалось" --label sync-failure --body $body }
```

- [ ] **Step 3: Список правок апстрима**

`FORK_CHANGES.md`:

```markdown
# Правки апстрима в форке aion2-overlay

Форк `drixed/aion2-overlay` от [Kuroukihime/AIon2-Dps-Meter](https://github.com/Kuroukihime/AIon2-Dps-Meter).
Чтобы слияния с апстримом проходили без конфликтов, файлы апстрима правятся только здесь:

| Файл | Правка |
|---|---|
| `AionDpsMeter.slnx` | +2 проекта: `AionDpsMeter.Timers`, `AionDpsMeter.Timers.Tests` |
| `AionDpsMeter.UI/AionDpsMeter.UI.csproj` | +1 `ProjectReference` на `AionDpsMeter.Timers` |
| `AionDpsMeter.UI/App.xaml.cs` | +2 строки с пометкой `// aion2-overlay fork`: `AddTimersOverlay(services)` и `TimersWindowController.Open()` |
| `AionDpsMeter.Services/Services/Update/UpdateCheckerService.cs` | `ReleasesApiUrl` → релизы `drixed/aion2-overlay` |

Новые файлы внутри проектов апстрима (с ними конфликтов не бывает, помечены первой строкой `aion2-overlay fork`):

- `AionDpsMeter.Services/PacketProcessing/Fork/IFieldBossListListener.cs`
- `AionDpsMeter.Services/PacketProcessing/Processors/FieldBossListForwarder.cs` — обработчик опкода `0x9101` (байты `01 91`)
- `AionDpsMeter.UI/Pages/TimersOverlay.razor`, `.razor.cs`, `.razor.css`
- `AionDpsMeter.UI/Services/TimerWindow/*`

Всё остальное — свои файлы: `AionDpsMeter.Timers/`, `AionDpsMeter.Timers.Tests/`, `schedule.json`,
`.github/workflows/sync-upstream.yml`, `docs/`.

## Если автообновление упало с конфликтом

1. `git fetch upstream && git merge upstream/master`
2. В конфликтующем файле оставить версию апстрима и заново внести нашу правку из таблицы выше.
3. `dotnet build AionDpsMeter.slnx -c Release && dotnet test AionDpsMeter.Timers.Tests`
4. `git commit` и `git push` — workflow сам соберёт релиз и закроет вопрос (issue закрыть вручную).

Если упал тест `Only_the_fork_forwarder_handles_the_field_boss_list_opcode` — апстрим сам начал обрабатывать
`01 91`: удалить `FieldBossListForwarder.cs` и подписаться на их обработчик.
```

- [ ] **Step 4: Инструкция для пользователя**

`docs/USAGE.ru.md`:

```markdown
# AION 2 оверлей — как пользоваться

## Установка
1. Нужен [Npcap](https://npcap.com/) (если уже стоит для другого метра — ничего делать не надо).
2. Скачать последний `Aion2Overlay-v…-win-x64.zip` со страницы
   [Releases](https://github.com/drixed/aion2-overlay/releases), распаковать в любую папку.
3. Запустить `AionDpsMeter.UI.exe`. Игра — в режиме «окно без рамки» (borderless), иначе оверлей не виден.

Дальше программа обновляется сама: при выходе новой версии она предложит обновиться.

## Окно таймеров
- Обычно окно «прозрачно» для мыши — клики проходят в игру.
- **Ctrl+Shift+T** — режим настройки: окно можно перетащить, включить/выключить типы событий,
  скрыть строку (×), отметить босса убитым («убит»). Ещё раз **Ctrl+Shift+T** — закрепить.
- **Полевые боссы** появляются, когда открываешь карту в игре: игра сама присылает, кто жив и когда респ.
  Если босса убили при тебе — отсчёт пойдёт от смерти (интервал программа запоминает после первого раза).
- **«(не проверено)»** у события — время взято не с EU. Сверь в игре и поправь `schedule.json` (ниже).
- Время оповещений и звук — в `timers-settings.json` рядом с exe.

## Расписание разломов и ивентов
Файл [`schedule.json`](../schedule.json) в репозитории. Правишь его на GitHub — программа подхватит
изменения при следующем запуске (или в течение 6 часов). Формат события:

- `"kind": "fixed"` — `times` (`"ЧЧ:ММ"`), необязательно `days` (`["Saturday"]`), `timeZone` (`"Europe/Berlin"`);
- `"kind": "interval"` — `anchor` (`"2026-10-07T20:00:00+02:00"`) и `periodMinutes`;
- `durationMinutes` — сколько идёт (0 — показывать только начало); `"verified": true` — убрать пометку.

## Если пришло письмо «Автообновление сломалось»
Это issue в репозитории: апстрим поменял что-то, что пересеклось с нашими правками. Напиши Claude —
инструкция для починки в `FORK_CHANGES.md`. Если в тексте ошибки есть `workflow` / `refusing to allow` —
апстрим поменял свои GitHub-workflow, и стандартному токену не хватает прав: создай токен
(GitHub → Settings → Developer settings → Personal access tokens (classic), права `repo` и `workflow`) и добавь
его в репозиторий как секрет `SYNC_TOKEN` (Settings → Secrets and variables → Actions). Токен вводишь сам — Claude
этого делать не будет.
```

- [ ] **Step 5: Собрать, прогнать тесты, закоммитить**

```bash
dotnet build AionDpsMeter.slnx -c Release && dotnet test AionDpsMeter.Timers.Tests
git add .github/workflows/sync-upstream.yml FORK_CHANGES.md docs/USAGE.ru.md AionDpsMeter.Services/Services/Update/UpdateCheckerService.cs
git commit -m "ci: daily upstream sync and release; updater points at the fork"
```

- [ ] **Step 6: Включить Actions и Issues в форке и запушить** (изменение настроек репозитория — спросить пользователя перед выполнением)

```bash
gh repo edit drixed/aion2-overlay --enable-issues
gh api -X PUT repos/drixed/aion2-overlay/actions/permissions -F enabled=true -f allowed_actions=all
git push -u origin master
```

- [ ] **Step 7: Дождаться первого релиза**

Push в `master` запускает workflow. Проверить:

```bash
gh run list --repo drixed/aion2-overlay --workflow sync-upstream.yml --limit 1
gh release list --repo drixed/aion2-overlay --limit 1
```
Expected: запуск `completed success`, релиз `v2026.….N` с zip-файлом. При падении — `gh run view --log-failed` и чинить.
Также `curl -fsSL https://raw.githubusercontent.com/drixed/aion2-overlay/master/schedule.json | head -3` — файл доступен.

- [ ] **Step 8: Передать пользователю проверку в игре**

Попросить пользователя: скачать zip из релиза, запустить, открыть карту с полевыми боссами, посмотреть
окно таймеров; сверить время разлома на EU и сообщить его (тогда обновить `schedule.json`, `"verified": true`).
Если боссы не появляются при открытой карте — попросить лог из папки программы (строки `Field boss list`).
```
