using AionDpsMeter.Timers.Feed;

namespace AionDpsMeter.Timers.Tests.Feed;

public class AlertServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 7, 10, 0, 0, TimeSpan.Zero);

    private static FeedItem Item(string id, FeedKind kind, DateTimeOffset at, FeedStatus status = FeedStatus.Upcoming) =>
        new(id, $"{id}@{at:O}", kind, id, null, status, at, true, null);

    private static int Lead(FeedKind kind) => kind == FeedKind.Boss ? 5 : 2;

    [Fact]
    public void Alerts_once_inside_the_lead_time()
    {
        var alerts = new AlertService();
        var boss = Item("boss", FeedKind.Boss, Now.AddMinutes(4));
        var rift = Item("rift", FeedKind.Rift, Now.AddMinutes(4)); // rift lead is 2 min: not yet

        Assert.Equal([boss], alerts.Due([boss, rift], Now, Lead));
        Assert.Empty(alerts.Due([boss, rift], Now.AddSeconds(1), Lead));
        Assert.Equal([rift], alerts.Due([boss, rift], Now.AddMinutes(2), Lead));
    }

    [Fact]
    public void A_new_spawn_time_alerts_again()
    {
        var alerts = new AlertService();
        Assert.Single(alerts.Due([Item("boss", FeedKind.Boss, Now.AddMinutes(3))], Now, Lead));
        Assert.Single(alerts.Due([Item("boss", FeedKind.Boss, Now.AddMinutes(4))], Now, Lead));
    }

    [Fact]
    public void Running_alive_and_past_items_never_alert()
    {
        var alerts = new AlertService();
        Assert.Empty(alerts.Due([
            Item("a", FeedKind.Boss, Now.AddMinutes(1), FeedStatus.Alive),
            Item("b", FeedKind.Rift, Now.AddMinutes(1), FeedStatus.Active),
            Item("c", FeedKind.Boss, Now.AddMinutes(-1), FeedStatus.Overdue),
        ], Now, Lead));
    }
}
