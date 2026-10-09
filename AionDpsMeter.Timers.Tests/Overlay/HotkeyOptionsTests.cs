using AionDpsMeter.Timers.Feed;

namespace AionDpsMeter.Timers.Tests.Overlay;

public class HotkeyOptionsTests
{
    [Fact]
    public void All_six_abyss_hotkeys_exist_unset_by_default_and_round_trip()
    {
        var o = new TimersOptions();
        Assert.All(new[] { o.HideOverlayHotkey, o.ResetFightHotkey, o.ClearMeterHotkey, o.CopySummaryHotkey, o.CompactHotkey, o.ClickThroughHotkey },
            h => Assert.Equal("", h));

        using var dir = new TempDir();
        var store = new TimersOptionsStore(dir.File("s.json"));
        store.Load();
        store.Current.HideOverlayHotkey = "Ctrl+H";
        store.Current.ClearMeterHotkey = "Ctrl+R";
        store.Current.CompactHotkey = "Alt+C";
        store.Current.ClickThroughHotkey = "Alt+T";
        store.Save();
        var again = new TimersOptionsStore(dir.File("s.json"));
        again.Load();
        Assert.Equal(("Ctrl+H", "Ctrl+R", "Alt+C", "Alt+T"),
            (again.Current.HideOverlayHotkey, again.Current.ClearMeterHotkey, again.Current.CompactHotkey, again.Current.ClickThroughHotkey));
    }

    [Fact]
    public void The_boss_timers_window_has_its_own_show_hide_hotkey()
    {
        Assert.Equal("", new TimersOptions().ToggleBossTimersHotkey);
        using var dir = new TempDir();
        var store = new TimersOptionsStore(dir.File("s.json"));
        store.Load();
        store.Current.ToggleBossTimersHotkey = "Alt+B";
        store.Save();
        var again = new TimersOptionsStore(dir.File("s.json"));
        again.Load();
        Assert.Equal("Alt+B", again.Current.ToggleBossTimersHotkey);
    }

    [Fact]
    public void The_boss_timers_window_can_be_turned_off_and_compact_mode_is_off_by_default()
    {
        var o = new TimersOptions();
        Assert.True(o.ShowBossTimers);
        Assert.False(o.Compact);
    }
}
