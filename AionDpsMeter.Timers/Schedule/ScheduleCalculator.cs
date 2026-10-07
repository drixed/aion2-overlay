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
