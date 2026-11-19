namespace AionDpsMeter.Timers.Feed;

public sealed class AlertService
{
    private readonly HashSet<string> fired = new();

    /// <summary>Upcoming items starting within their lead time. Each occurrence (Key) is returned once.</summary>
    /// <param name="leadMinutes">Minutes ahead for each item (each event can have its own).</param>
    public IReadOnlyList<FeedItem> Due(IEnumerable<FeedItem> items, DateTimeOffset now, Func<FeedItem, int> leadMinutes)
    {
        var due = new List<FeedItem>();
        foreach (var item in items)
        {
            if (item.Status != FeedStatus.Upcoming || item.At is not { } at) continue;
            var lead = leadMinutes(item);
            if (lead <= 0 || at - now > TimeSpan.FromMinutes(lead)) continue;
            if (fired.Add(item.Key)) due.Add(item);
        }
        return due;
    }

    /// <summary>Events that are on now ("при открытии"), each occurrence once.</summary>
    public IReadOnlyList<FeedItem> Opened(IEnumerable<FeedItem> items) =>
        items.Where(i => i.Status == FeedStatus.Active && fired.Add("open:" + i.Key)).ToList();

    /// <summary>Open events ending within <paramref name="seconds"/> ("перед закрытием"), each occurrence once; 0 is off.</summary>
    public IReadOnlyList<FeedItem> Closing(IEnumerable<FeedItem> items, DateTimeOffset now, int seconds)
    {
        if (seconds <= 0) return [];
        return items.Where(i => i.Status == FeedStatus.Active && i.At is { } end && end > now && end - now <= TimeSpan.FromSeconds(seconds)
                && fired.Add("close:" + i.Key)).ToList();
    }
}
