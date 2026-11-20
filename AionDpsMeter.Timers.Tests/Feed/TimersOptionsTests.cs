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
    public void Map_window_settings_are_kept_and_an_old_file_gets_the_defaults()
    {
        using var dir = new TempDir();
        var path = dir.File("timers-settings.json");
        File.WriteAllText(path, "{ \"BossLeadMinutes\": 7 }");   // written before the map had settings
        var store = new TimersOptionsStore(path);
        store.Load();
        var map = store.Current.Map;
        Assert.True(map.Sidebar);
        Assert.Equal(100, map.Opacity);
        Assert.Equal(["Herb", "Ore"], map.Kinds.Order());

        map.Sidebar = false;
        map.Opacity = 55;
        map.PointSize = 60;
        map.BossNames = false;
        map.Kinds.Add("Wood");
        store.Save();

        var again = new TimersOptionsStore(path);
        again.Load();
        Assert.False(again.Current.Map.Sidebar);
        Assert.Equal(55, again.Current.Map.Opacity);
        Assert.Equal(0.6, again.Current.Map.PointScale, 3);
        Assert.False(again.Current.Map.BossNames);
        Assert.Contains("Wood", again.Current.Map.Kinds);
    }

    [Fact]
    public void The_healing_log_is_off_until_turned_on_and_follows_the_setting()
    {
        using var dir = new TempDir();
        var store = new TimersOptionsStore(dir.File("timers-settings.json"));
        store.Load();
        Assert.False(store.Current.HealProbe);
        store.ApplyDiagnostics();
        Assert.False(AionDpsMeter.Services.PacketProcessing.Fork.HealProbe.Enabled);

        store.Current.HealProbe = true;
        store.Save();                                   // saving applies it at once
        Assert.True(AionDpsMeter.Services.PacketProcessing.Fork.HealProbe.Enabled);

        store.Current.HealProbe = false;
        store.Save();
        Assert.False(AionDpsMeter.Services.PacketProcessing.Fork.HealProbe.Enabled);
    }

    [Fact]
    public void Map_opacity_and_point_size_stay_in_range()
    {
        var map = new MapOptions { Opacity = 3, PointSize = 900 };
        Assert.Equal(MapOptions.MinOpacity, map.ClampedOpacity);
        Assert.Equal(MapOptions.MaxPointSize / 100.0, map.PointScale, 3);
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
    public void The_fight_card_is_for_bosses_unless_asked_otherwise()
    {
        Assert.False(new TimersOptions().CardForAllTargets);
    }

    [Fact]
    public void Each_event_can_be_muted_on_its_own()
    {
        var o = new TimersOptions();
        var rift = new FeedItem("rift", "k", FeedKind.Rift, "Разлом", null, FeedStatus.Upcoming, null, true, null);
        var shugo = new FeedItem("shugo-festival", "k", FeedKind.Event, "Фестиваль шуго", null, FeedStatus.Upcoming, null, true, null);
        var boss = new FeedItem("boss:2400800", "k", FeedKind.Boss, "Гартуа", null, FeedStatus.Upcoming, null, true, 2400800);
        Assert.True(o.Notifies(rift) && o.Notifies(shugo) && o.Notifies(boss));

        o.MutedIds.Add("shugo-festival");
        Assert.False(o.Notifies(shugo));
        Assert.True(o.Notifies(rift));
        Assert.True(o.Shows(shugo)); // muted, still shown

        o.MutedIds.Add(TimersOptions.AllFieldBosses);
        Assert.False(o.Notifies(boss));

        o.RiftLeadMinutes = 0;
        Assert.False(o.Notifies(rift)); // 0 minutes = no alert
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
    [Fact]
    public void An_old_hide_all_bosses_setting_is_dropped_the_timers_window_is_all_bosses()
    {
        using var dir = new TempDir();
        var path = dir.File("timers-settings.json");
        File.WriteAllText(path, """{ "HiddenKinds": ["Boss", "Rift"] }""");
        var store = new TimersOptionsStore(path);
        store.Load();
        Assert.DoesNotContain(FeedKind.Boss, store.Current.HiddenKinds);
        Assert.Contains(FeedKind.Rift, store.Current.HiddenKinds);
    }
    private static FeedItem Boss(int key) => new($"boss:{key}", "k", FeedKind.Boss, "B", null, FeedStatus.Upcoming, null, true, key);

    [Fact]
    public void Switched_on_bosses_are_the_only_ones_shown_and_alerted_none_switched_on_means_all()
    {
        var o = new TimersOptions();
        Assert.True(o.Watches(Boss(2100877)));
        Assert.True(o.Notifies(Boss(2100877)));

        o.WatchedBosses.Add("boss:2100877");
        Assert.True(o.Watches(Boss(2100877)));
        Assert.False(o.Watches(Boss(2100003)));
        Assert.False(o.Notifies(Boss(2100003)));
        var rift = Boss(1) with { Id = "rift", Kind = FeedKind.Rift, BossKey = null };
        Assert.True(o.Watches(rift)); // schedule events are not field bosses
    }

    [Fact]
    public void A_boss_can_have_its_own_sound_and_volume_else_the_field_boss_ones()
    {
        var o = new TimersOptions { AlertVolume = 70 };
        var own = AlertSound.System(WindowsSound.Beep);
        var all = AlertSound.System(WindowsSound.Hand);
        o.SetSound(TimersOptions.AllFieldBosses, all);
        Assert.Equal(all, o.SoundFor(Boss(2100877)));
        o.SetSound("boss:2100877", own);
        Assert.Equal(own, o.SoundFor(Boss(2100877)));
        Assert.Equal(all, o.SoundFor(Boss(2100003)));

        Assert.Equal(70, o.VolumeFor(Boss(2100877)));
        o.Volumes["boss:2100877"] = 30;
        Assert.Equal(30, o.VolumeFor(Boss(2100877)));
        Assert.Equal(70, o.VolumeFor(Boss(2100003)));
    }
}
