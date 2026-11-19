// aion2-overlay fork: new file (see FORK_CHANGES.md).
using AionDpsMeter.Timers.Schedule;

namespace AionDpsMeter.Timers.Overlay;

/// <summary>
/// A floating panel of event icons over the game (Шуго, Разлом, …): its own window, placed anywhere on screen, with
/// its own events, look and size. Kept in timers-settings.json.
/// </summary>
public sealed class EventPanel
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "Панель";
    public List<string> EventIds { get; set; } = [];
    /// <summary>"cards" (icon, time, name under it) or "bars" (a row with a progress fill).</summary>
    public string Look { get; set; } = "cards";
    /// <summary>"square" or "circle" icons.</summary>
    public string Shape { get; set; } = "square";
    public bool Vertical { get; set; }
    public int IconSize { get; set; } = 36;
    public int Spacing { get; set; } = 4;
    public int Opacity { get; set; } = 100;
    /// <summary>"system", "narrow", "mono" or "wide", like the main window's fonts.</summary>
    public string Font { get; set; } = "system";
    /// <summary>The name's size in px; the time is 4 px bigger.</summary>
    public int TextSize { get; set; } = 11;
    public bool Visible { get; set; } = true;
    /// <summary>Screen position; null until the user places it.</summary>
    public double? Left { get; set; }
    public double? Top { get; set; }

    public static EventPanel Create(string name, IEnumerable<string> eventIds) =>
        new() { Id = Guid.NewGuid().ToString("N")[..12], Name = name, EventIds = eventIds.ToList() };
}

/// <param name="Countdown">To the start while waiting, to the end while open: "49:59", "2:49:59".</param>
/// <param name="Progress">How much of an open event has passed, 0–1; 0 while waiting.</param>
public sealed record EventTile(string Id, string Name, string Icon, ChipState State, string Countdown, double Progress);

public static class EventPanels
{
    public const string BossPrefix = "boss:";

    /// <summary>A field boss on a panel is kept as "boss:{key}" among the event ids.</summary>
    public static string BossId(int key) => BossPrefix + key;

    private static readonly TimeSpan BossSoon = TimeSpan.FromMinutes(5);

    /// <summary>The panel's tiles in its own order; events the schedule no longer has, and bosses the meter does not know
    /// on this server, are left out.</summary>
    public static IReadOnlyList<EventTile> Tiles(DateTimeOffset now, ScheduleData data, EventPanel panel,
        IReadOnlyCollection<AionDpsMeter.Timers.Bosses.BossTimer>? bosses = null, Func<int, string>? bossName = null)
    {
        var tiles = new List<EventTile>();
        foreach (var id in panel.EventIds)
        {
            if (id.StartsWith(BossPrefix, StringComparison.Ordinal))
            {
                // An unnamed map place is kept as "boss:-{place}"; once named, the same place is found by its slot.
                if (int.TryParse(id[BossPrefix.Length..], out var key)
                    && (bosses?.FirstOrDefault(b => b.Key == key) ?? (key < 0 ? bosses?.FirstOrDefault(b => b.SlotId == -key) : null)) is { } boss)
                    tiles.Add(BossTile(now, id, boss.Key > 0 ? bossName?.Invoke(boss.Key) ?? $"Босс {boss.Key}" : $"Место {boss.SlotId % 100}", boss));
                continue;
            }
            if (data.Events.FirstOrDefault(e => e.Id == id) is not { } e || ScheduleCalculator.Next(e, now) is not { } o) continue;
            var open = o.IsActive(now) && o.End > o.Start;
            var toStart = o.Start - now;
            var entryLeft = o.Start + e.Entry - now;
            if (open && entryLeft > TimeSpan.Zero) // only the entry is open yet (game: "Войти" with a countdown)
            {
                tiles.Add(new EventTile(e.Id, e.Name, e.Icon ?? "event", ChipState.Soon, "вход " + Clock(entryLeft), 0));
                continue;
            }
            var state = open ? ChipState.Open
                : toStart > TimeSpan.Zero && toStart <= TimeSpan.FromMinutes(e.SoonMinutes) ? ChipState.Soon
                : ChipState.Idle;
            var progress = open ? (now - o.Start) / (o.End - o.Start) : 0;
            tiles.Add(new EventTile(e.Id, e.Name, e.Icon ?? "event", state, Clock(open ? o.End - now : toStart), progress));
        }
        return tiles;
    }

    /// <summary>Up: "жив". Waiting: the respawn countdown, glowing in its last 5 minutes. Past its time with no news from
    /// the game yet: "скоро". Never seen on the map: "—".</summary>
    private static EventTile BossTile(DateTimeOffset now, string id, string name, AionDpsMeter.Timers.Bosses.BossTimer b)
    {
        if (b.Alive) return new EventTile(id, name, "boss", ChipState.Open, "жив", 0);
        if (b.NextSpawn is { } next && next > now)
            return new EventTile(id, name, "boss", next - now <= BossSoon ? ChipState.Soon : ChipState.Idle, Clock(next - now), 0);
        if (b.NextSpawn is { } passed && now - passed < AionDpsMeter.Timers.Feed.TimersFeed.OverdueWindow)
            return new EventTile(id, name, "boss", ChipState.Soon, "скоро", 0);
        return new EventTile(id, name, "boss", ChipState.Idle, "—", 0);
    }

    /// <summary>The panel window's size for its tiles: a card is an icon with the time and a two-line name under it, a
    /// bar is a row with the icon, name, time and a progress line; "Вертикально" stacks them.</summary>
    public static (double Width, double Height) WindowSize(EventPanel p)
    {
        const double pad = 12;
        var n = Math.Max(1, p.EventIds.Count);
        var text = Math.Clamp(p.TextSize, 8, 24);
        double tileW, tileH;
        if (p.Look == "bars")
        {
            tileW = 150 + p.IconSize + text * 5;
            tileH = Math.Max(p.IconSize, (text + 2) * 1.4) + 10;
        }
        else
        {
            tileW = Math.Max(p.IconSize + 16, text * 8.4);
            tileH = p.IconSize + 5 + (text + 4) * 1.3 + text * 1.2 * 2;
        }
        return p.Vertical
            ? (tileW + pad, n * tileH + (n - 1) * p.Spacing + pad)
            : (n * tileW + (n - 1) * p.Spacing + pad, tileH + pad);
    }

    /// <summary>"m:ss" under an hour, "h:mm:ss" from an hour, like the game's own timers.</summary>
    public static string Clock(TimeSpan t)
    {
        var s = Math.Max(0, (long)Math.Floor(t.TotalSeconds));
        return s >= 3600 ? $"{s / 3600}:{s % 3600 / 60:00}:{s % 60:00}" : $"{s / 60}:{s % 60:00}";
    }
}
