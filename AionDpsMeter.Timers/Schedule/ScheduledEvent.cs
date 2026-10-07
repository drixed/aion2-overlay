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
