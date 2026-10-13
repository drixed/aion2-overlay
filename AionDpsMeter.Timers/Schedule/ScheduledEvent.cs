namespace AionDpsMeter.Timers.Schedule;

public enum ScheduleKind { Fixed, Interval }

/// <summary>One event from schedule.json. Fixed: <see cref="Times"/> of day (optionally on <see cref="Days"/>) in
/// <see cref="TimeZone"/>. Interval: <see cref="Anchor"/> + k × <see cref="Period"/>. Icon picks the main window's
/// event icon; AlertByDefault=false keeps an event quiet until the user switches its alert on; SoonMinutes: how long
/// before the start its icon glows.</summary>
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
    bool Verified,
    IReadOnlyList<TimeSpan>? Durations = null,
    string? Icon = null,
    bool AlertByDefault = true,
    int SoonMinutes = 2)
{
    /// <summary>Fixed events: the length of the window starting at Times[i] (Arena of Tactics: 3 h, then 2 h).</summary>
    public TimeSpan DurationAt(int index) =>
        Durations is { } d && d.Count == Times.Count ? d[index] : Duration;
}

public readonly record struct Occurrence(DateTimeOffset Start, DateTimeOffset End)
{
    public bool IsActive(DateTimeOffset now) => Start <= now && now < End;
}
