using AionDpsMeter.Timers.Feed;

namespace AionDpsMeter.Timers.Tests.Feed;

public class TimersOptionsTests
{
    [Fact]
    public void Options_round_trip_and_filter()
    {
        using var dir = new TempDir();
        var store = new TimersOptionsStore(dir.File("timers-settings.json"));
        store.Load();
        Assert.Equal(5, store.Current.LeadFor(FeedKind.Boss));
        store.Current.HiddenKinds.Add(FeedKind.Event);
        store.Current.HiddenIds.Add("boss:2400800");
        store.Current.RiftLeadMinutes = 3;
        store.Save();

        var again = new TimersOptionsStore(dir.File("timers-settings.json"));
        again.Load();
        Assert.Equal(3, again.Current.LeadFor(FeedKind.Rift));
        var item = new FeedItem("boss:2400800", "k", FeedKind.Boss, "B", null, FeedStatus.Upcoming, null, true, 2400800);
        Assert.False(again.Current.Shows(item));
        Assert.False(again.Current.Shows(item with { Id = "x", Kind = FeedKind.Event }));
        Assert.True(again.Current.Shows(item with { Id = "x" }));
    }

    [Fact]
    public void A_corrupt_settings_file_gives_defaults()
    {
        using var dir = new TempDir();
        File.WriteAllText(dir.File("timers-settings.json"), "{ nope");
        var store = new TimersOptionsStore(dir.File("timers-settings.json"));
        store.Load();
        Assert.Equal(2, store.Current.RiftLeadMinutes);
    }
}
