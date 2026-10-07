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
