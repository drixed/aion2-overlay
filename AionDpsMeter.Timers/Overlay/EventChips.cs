using AionDpsMeter.Timers.Feed;
using AionDpsMeter.Timers.Schedule;

namespace AionDpsMeter.Timers.Overlay;

public enum ChipState { Idle, Soon, Open }

/// <param name="Text">Minutes left while open, minutes to the start while it glows; empty otherwise.</param>
/// <param name="Tooltip">Name and the exact time, shown on hover.</param>
public sealed record EventChip(string Id, string Name, string Icon, ChipState State, string Text, string Tooltip);

/// <summary>The row of event icons in the main window: idle, glowing shortly before the start, lit while open.</summary>
public static class EventChips
{
    /// <param name="alwaysTime">Show the countdown next to a waiting event too, not only shortly before it.</param>
    public static IReadOnlyList<EventChip> Build(DateTimeOffset now, ScheduleData data, TimeZoneInfo? local = null, bool alwaysTime = false)
    {
        local ??= TimeZoneInfo.Local;
        var chips = new List<EventChip>();
        foreach (var e in data.Events)
        {
            if (ScheduleCalculator.Next(e, now) is not { } o) continue;
            var open = o.IsActive(now) && o.End > o.Start;
            var toStart = o.Start - now;
            var soon = !open && toStart > TimeSpan.Zero && toStart <= TimeSpan.FromMinutes(e.SoonMinutes);
            var state = open ? ChipState.Open : soon ? ChipState.Soon : ChipState.Idle;
            var text = state switch
            {
                ChipState.Open => Minutes(o.End - now),
                ChipState.Soon => Minutes(toStart),
                _ when alwaysTime => Minutes(toStart),
                _ => "",
            };
            var unverified = e.Verified ? "" : " (не проверено)";
            var tooltip = open
                ? $"{e.Name}{unverified} — идёт, ещё {FeedText.Span(o.End - now)}"
                : $"{e.Name}{unverified} — через {FeedText.Span(toStart)} (в {TimeZoneInfo.ConvertTime(o.Start, local):HH:mm})";
            chips.Add(new EventChip(e.Id, e.Name, e.Icon ?? DefaultIcon(e), state, text, tooltip));
        }
        return chips;
    }

    private static string Minutes(TimeSpan t)
    {
        var minutes = Math.Max(1, (int)Math.Ceiling(t.TotalMinutes));
        return minutes < 60 ? $"{minutes}м" : $"{minutes / 60}ч{minutes % 60:00}м";
    }

    private static string DefaultIcon(ScheduledEvent e) =>
        e.Category.Equals("rift", StringComparison.OrdinalIgnoreCase) ? "rift"
        : e.Id.Contains("shugo", StringComparison.OrdinalIgnoreCase) ? "shugo"
        : "event";
}
