namespace AionDpsMeter.Timers.Feed;

public enum FeedKind { Rift, Event, Boss }

public enum FeedStatus { Active, Alive, Upcoming, Overdue, Unknown }

/// <param name="Id">Stable across occurrences (used to hide an item): the schedule event id, or "boss:{key}".</param>
/// <param name="Key">One occurrence (used to alert once): Id + time.</param>
/// <param name="At">Active: ends at. Alive: alive since. Upcoming / Overdue: starts or spawns at.</param>
/// <param name="BossKey">The tracker key for bosses (for the "убит" button); null for schedule events.</param>
public sealed record FeedItem(
    string Id,
    string Key,
    FeedKind Kind,
    string Title,
    string? Zone,
    FeedStatus Status,
    DateTimeOffset? At,
    bool Verified,
    int? BossKey);
