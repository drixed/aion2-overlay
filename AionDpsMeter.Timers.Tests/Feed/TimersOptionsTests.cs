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
    public void Startup_creates_a_missing_file_but_never_overwrites_a_hand_edited_one()
    {
        using var dir = new TempDir();
        var path = dir.File("timers-settings.json");

        new TimersOptionsStore(path).LoadOrCreate();
        Assert.True(File.Exists(path));

        File.WriteAllText(path, "{ \"BossLeadMinutes\": 7, typo }");
        var store = new TimersOptionsStore(path);
        store.LoadOrCreate();
        Assert.Equal(5, store.Current.BossLeadMinutes); // defaults in memory…
        Assert.Equal("{ \"BossLeadMinutes\": 7, typo }", File.ReadAllText(path)); // …but the user's file survives
    }

    [Fact]
    public void Window_state_and_dismissed_notices_round_trip()
    {
        using var dir = new TempDir();
        var store = new TimersOptionsStore(dir.File("timers-settings.json"));
        store.Load();
        Assert.False(store.Current.Collapsed);
        Assert.False(store.Current.IsDismissed("patch-1"));
        store.Current.Collapsed = true;
        store.Current.DismissedNotices.Add("patch-1");
        store.Save();

        var again = new TimersOptionsStore(dir.File("timers-settings.json"));
        again.Load();
        Assert.True(again.Current.Collapsed);
        Assert.True(again.Current.IsDismissed("patch-1"));
        Assert.False(again.Current.IsDismissed("patch-2"));
    }

    [Fact]
    public void The_boss_panel_is_the_default_main_style()
    {
        var options = new TimersOptions();
        Assert.True(options.UseBossPanel);
        options.MainStyle = "upstream";
        Assert.False(options.UseBossPanel);

        using var dir = new TempDir();
        var store = new TimersOptionsStore(dir.File("timers-settings.json"));
        store.Load();
        store.Save();
        Assert.DoesNotContain("UseBossPanel", File.ReadAllText(dir.File("timers-settings.json")));
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
