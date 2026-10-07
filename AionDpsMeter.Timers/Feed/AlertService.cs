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
