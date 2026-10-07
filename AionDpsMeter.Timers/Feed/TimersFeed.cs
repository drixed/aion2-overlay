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
