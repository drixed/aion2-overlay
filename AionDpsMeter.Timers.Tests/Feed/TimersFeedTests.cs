using AionDpsMeter.Timers.Bosses;
using AionDpsMeter.Timers.Feed;
using AionDpsMeter.Timers.Schedule;

namespace AionDpsMeter.Timers.Tests.Feed;

public class TimersFeedTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 7, 10, 30, 0, TimeSpan.Zero);
    private static readonly BossCatalog Catalog = BossCatalog.FromJson("""{ "2400800": { "name": "Гартуа", "isBoss": true } }""");

    private static ScheduleData Data(params ScheduledEvent[] events) =>
        new(events, new Dictionary<int, FieldBossMapInfo> { [1110] = new(2400, "Альтгард") }, new Dictionary<int, int>());

    private static ScheduledEvent Rift(bool verified = false, int duration = 0) =>
        new("rift", "Разлом", "rift", ScheduleKind.Fixed, TimeZoneInfo.Utc, [new TimeOnly(11, 0)], null, null, null,
            TimeSpan.FromMinutes(duration), verified);

    private static BossTimer Boss(int key, bool alive = false, DateTimeOffset? next = null, DateTimeOffset? since = null) =>
        new(1, key, 1110, 111021, alive, since, next, null, null);

    [Fact]
    public void Schedule_events_become_upcoming_items_marked_unverified()
    {
        var item = Assert.Single(TimersFeed.Build(Now, Data(Rift()), [], Catalog));
        Assert.Equal(FeedKind.Rift, item.Kind);
        Assert.Equal(FeedStatus.Upcoming, item.Status);
        Assert.Equal(Now.AddMinutes(30), item.At);
        Assert.False(item.Verified);
        Assert.Equal("rift", item.Id);
    }

    [Fact]
    public void A_running_event_is_active_until_its_end()
    {
        var item = Assert.Single(TimersFeed.Build(Now.AddMinutes(40), Data(Rift(duration: 60)), [], Catalog));
        Assert.Equal(FeedStatus.Active, item.Status);
        Assert.Equal(Now.AddMinutes(90), item.At);
    }

    [Fact]
    public void Boss_statuses_follow_the_timer()
    {
        var items = TimersFeed.Build(Now, Data(), [
            Boss(2400800, next: Now.AddMinutes(20)),
            Boss(2400003, alive: true, since: Now.AddMinutes(-3)),
            Boss(2400004, next: Now.AddMinutes(-10)),
            Boss(2400005, next: Now.AddHours(-3)),
        ], Catalog);

        Assert.Equal(FeedStatus.Upcoming, items.Single(i => i.BossKey == 2400800).Status);
        Assert.Equal("Гартуа", items.Single(i => i.BossKey == 2400800).Title);
        Assert.Equal("Альтгард", items.Single(i => i.BossKey == 2400800).Zone);
        Assert.Equal(FeedStatus.Alive, items.Single(i => i.BossKey == 2400003).Status);
        Assert.Equal(FeedStatus.Overdue, items.Single(i => i.BossKey == 2400004).Status);
        Assert.Equal(FeedStatus.Unknown, items.Single(i => i.BossKey == 2400005).Status);
    }

    [Fact]
    public void Items_are_ordered_running_first_then_soonest()
    {
        var items = TimersFeed.Build(Now, Data(Rift()), [
            Boss(2400001, next: Now.AddMinutes(5)),
            Boss(2400002, alive: true, since: Now.AddMinutes(-1)),
            Boss(2400003, next: Now.AddHours(-5)),
        ], Catalog);
        Assert.Equal([2400002, 2400001, null, 2400003], items.Select(i => i.BossKey));
    }

    [Fact]
    public void Unnamed_bosses_collapse_into_one_row_per_map()
    {
        var items = TimersFeed.BuildBosses(Now, Data(), [
            new BossTimer(1, -101001, 1010, 101001, true, null, null, null, null),
            new BossTimer(1, -101002, 1010, 101002, true, null, null, null, null),
            new BossTimer(1, -101003, 1010, 101003, false, null, Now.AddMinutes(7), null, null),
            new BossTimer(1, 2400800, 1110, 111021, false, null, Now.AddMinutes(20), null, null),
        ], Catalog);

        Assert.Equal(2, items.Count);
        var group = items.Single(i => i.Id == "map:1010");
        Assert.Equal("Карта 1010: живы 2 из 3", group.Title);
        Assert.Equal(FeedStatus.Upcoming, group.Status);
        Assert.Equal(Now.AddMinutes(7), group.At);
        Assert.Null(group.BossKey);
        Assert.Equal("Альтгард: живы 1 из 1", TimersFeed.BuildBosses(Now, Data(),
            [new BossTimer(1, -111001, 1110, 111001, true, null, null, null, null)], Catalog).Single().Title);
    }

    [Fact]
    public void Schedule_and_bosses_can_be_built_separately()
    {
        Assert.Equal(["rift"], TimersFeed.BuildSchedule(Now, Data(Rift())).Select(i => i.Id));
        Assert.Empty(TimersFeed.BuildBosses(Now, Data(Rift()), [], Catalog));
    }

    [Fact]
    public void Text_formats()
    {
        var local = TimeZoneInfo.CreateCustomTimeZone("UTC+3", TimeSpan.FromHours(3), "UTC+3", "UTC+3");
        Assert.Equal("1:02:03", FeedText.Span(new TimeSpan(1, 2, 3)));
        Assert.Equal("1:05", FeedText.Span(TimeSpan.FromSeconds(65)));
        Assert.Equal("0:00", FeedText.Span(TimeSpan.FromSeconds(-5)));
        var alive = new FeedItem("b", "b", FeedKind.Boss, "B", null, FeedStatus.Alive, Now, true, 1);
        Assert.Equal("жив с 13:30", FeedText.Format(alive, Now, local));
        var soon = alive with { Status = FeedStatus.Upcoming, At = Now.AddMinutes(90) };
        Assert.Equal("через 1:30:00", FeedText.Format(soon, Now, local));
        Assert.Equal("?", FeedText.Format(alive with { Status = FeedStatus.Unknown, At = null }, Now, local));
    }
}
