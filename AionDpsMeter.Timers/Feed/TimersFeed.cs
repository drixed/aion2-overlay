using AionDpsMeter.Timers.Bosses;
using AionDpsMeter.Timers.Schedule;

namespace AionDpsMeter.Timers.Feed;

public static class TimersFeed
{
    /// <summary>How long a boss stays "должен быть" after its spawn time before it turns into "?".</summary>
    public static readonly TimeSpan OverdueWindow = TimeSpan.FromMinutes(60);

    public static IReadOnlyList<FeedItem> Build(DateTimeOffset now, ScheduleData data, IEnumerable<BossTimer> bosses, BossCatalog catalog) =>
        Sort(BuildSchedule(now, data).Concat(BuildBosses(now, data, bosses, catalog)));

    public static IReadOnlyList<FeedItem> BuildSchedule(DateTimeOffset now, ScheduleData data)
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
        return Sort(items);
    }

    /// <summary>Named bosses one row each; bosses of a map not in the tables (unknown names) one summary row per map.</summary>
    public static IReadOnlyList<FeedItem> BuildBosses(DateTimeOffset now, ScheduleData data, IEnumerable<BossTimer> bosses, BossCatalog catalog)
    {
        var items = new List<FeedItem>();
        var list = bosses.ToList();
        foreach (var b in list.Where(b => b.Key > 0))
        {
            var (status, at) = b switch
            {
                { Alive: true } => (FeedStatus.Alive, b.AliveSince),
                { NextSpawn: { } next } when next > now => (FeedStatus.Upcoming, (DateTimeOffset?)next),
                { NextSpawn: { } next } when now - next < OverdueWindow => (FeedStatus.Overdue, (DateTimeOffset?)next),
                _ => (FeedStatus.Unknown, (DateTimeOffset?)null),
            };
            items.Add(new FeedItem($"boss:{b.Key}", $"boss:{b.Key}@{at:O}", FeedKind.Boss, catalog.Name(b.Key),
                ZoneOf(data, b.MapId), status, at, true, b.Key));
        }
        foreach (var map in list.Where(b => b.Key < 0).GroupBy(b => b.MapId))
        {
            var alive = map.Count(b => b.Alive);
            var upcoming = map.Where(b => !b.Alive && b.NextSpawn > now).Select(b => b.NextSpawn!.Value).ToList();
            DateTimeOffset? next = upcoming.Count > 0 ? upcoming.Min() : null;
            var status = next is not null ? FeedStatus.Upcoming : alive > 0 ? FeedStatus.Alive : FeedStatus.Unknown;
            var title = $"{ZoneOf(data, map.Key) ?? $"Карта {map.Key}"}: живы {alive} из {map.Count()}";
            items.Add(new FeedItem($"map:{map.Key}", $"map:{map.Key}@{next:O}", FeedKind.Boss, title, null, status, next, true, null));
        }
        return Sort(items);
    }

    private static string? ZoneOf(ScheduleData data, int mapId) =>
        data.FieldBossMaps.TryGetValue(mapId, out var map) ? map.Name : null;

    private static IReadOnlyList<FeedItem> Sort(IEnumerable<FeedItem> items) => items
        .OrderBy(i => Rank(i.Status))
        .ThenBy(i => i.At ?? DateTimeOffset.MaxValue)
        .ThenBy(i => i.Title, StringComparer.CurrentCulture)
        .ToList();

    private static int Rank(FeedStatus status) => status switch
    {
        FeedStatus.Active or FeedStatus.Alive => 0,
        FeedStatus.Upcoming => 1,
        FeedStatus.Overdue => 2,
        _ => 3,
    };
}
