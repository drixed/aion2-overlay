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
    /// <summary>The panel's tiles in its own order; events the schedule no longer has are left out.</summary>
    public static IReadOnlyList<EventTile> Tiles(DateTimeOffset now, ScheduleData data, EventPanel panel)
    {
        var tiles = new List<EventTile>();
        foreach (var id in panel.EventIds)
        {
            if (data.Events.FirstOrDefault(e => e.Id == id) is not { } e || ScheduleCalculator.Next(e, now) is not { } o) continue;
            var open = o.IsActive(now) && o.End > o.Start;
            var toStart = o.Start - now;
            var state = open ? ChipState.Open
                : toStart > TimeSpan.Zero && toStart <= TimeSpan.FromMinutes(e.SoonMinutes) ? ChipState.Soon
                : ChipState.Idle;
            var progress = open ? (now - o.Start) / (o.End - o.Start) : 0;
            tiles.Add(new EventTile(e.Id, e.Name, e.Icon ?? "event", state, Clock(open ? o.End - now : toStart), progress));
        }
        return tiles;
    }

    /// <summary>"m:ss" under an hour, "h:mm:ss" from an hour, like the game's own timers.</summary>
    public static string Clock(TimeSpan t)
    {
        var s = Math.Max(0, (long)Math.Floor(t.TotalSeconds));
        return s >= 3600 ? $"{s / 3600}:{s % 3600 / 60:00}:{s % 60:00}" : $"{s / 60}:{s % 60:00}";
    }
}
