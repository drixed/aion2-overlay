using AionDpsMeter.Timers.Feed;
using AionDpsMeter.Timers.Overlay;

namespace AionDpsMeter.Timers.Tests.Overlay;

public class OverlayLogicTests
{
    private static readonly DateTime T0 = new(2026, 10, 7, 20, 0, 0);

    [Fact]
    public void Active_dps_counts_only_the_time_between_first_and_last_hit()
    {
        // 60 s fight, but this player hit for only 20 s: eDPS 1000/s, aDPS 3000/s.
        Assert.Equal(3000, DpsMath.Active(60_000, T0, T0.AddSeconds(20)));
        Assert.Equal(60_000, DpsMath.Active(60_000, T0, T0)); // a single hit counts as one second
        Assert.Equal(0, DpsMath.Active(0, T0, T0.AddSeconds(5)));
    }

    [Fact]
    public void The_summary_fits_one_chat_line_or_one_line_per_player()
    {
        var players = new[] { new SummaryLine("DRIXXXED", 9_670, 62.4), new SummaryLine("Player", 5_100, 37.6) };
        Assert.Equal("Келпина 2:13 · DRIXXXED 9.67K/s 62% · Player 5.1K/s 38%",
            FightSummary.Format("Келпина", TimeSpan.FromSeconds(133), players, multiline: false));
        Assert.Equal("Келпина 2:13\nDRIXXXED 9.67K/s 62%\nPlayer 5.1K/s 38%",
            FightSummary.Format("Келпина", TimeSpan.FromSeconds(133), players, multiline: true));
        Assert.Equal("1.23M", FightSummary.Short(1_234_567));
        Assert.Equal("950", FightSummary.Short(950));
    }

    [Theory]
    [InlineData("AION2", true)]
    [InlineData("aion2", true)]
    [InlineData("AionDpsMeter.UI", true)]
    [InlineData("msedgewebview2", true)]
    [InlineData("browser", false)]
    [InlineData(null, false)]
    public void The_overlay_stays_on_top_while_the_game_or_the_meter_is_in_front(string? foreground, bool onTop)
    {
        Assert.Equal(onTop, OverlayFocus.StaysOnTop(foreground));
    }

    [Fact]
    public void New_overlay_options_have_abyss_like_defaults_and_round_trip()
    {
        var o = new TimersOptions();
        Assert.Equal(DpsMode.Effective, o.DpsMode);
        Assert.False(o.HideTotalDamage);
        Assert.False(o.OnlyMyParty);
        Assert.True(o.ShowPartyDps);
        Assert.True(o.ShowEnrage);
        Assert.False(o.SummaryMultiline);
        Assert.False(o.FocusMode);
        Assert.False(o.OnlyOverGame);
        Assert.Equal(100, o.UiScale);
        Assert.Equal("", o.ResetFightHotkey);
        Assert.Equal("", o.CopySummaryHotkey);

        using var dir = new TempDir();
        var store = new TimersOptionsStore(dir.File("s.json"));
        store.Load();
        store.Current.DpsMode = DpsMode.Active;
        store.Current.UiScale = 125;
        store.Current.ResetFightHotkey = "Ctrl+Shift+R";
        store.Save();
        var again = new TimersOptionsStore(dir.File("s.json"));
        again.Load();
        Assert.Equal(DpsMode.Active, again.Current.DpsMode);
        Assert.Equal(125, again.Current.UiScale);
        Assert.Equal("Ctrl+Shift+R", again.Current.ResetFightHotkey);
    }

    [Theory]
    [InlineData("r", true, true, false, "Ctrl+Shift+R")]
    [InlineData("F9", false, false, true, "Alt+F9")]
    [InlineData("1", true, false, false, "Ctrl+D1")]
    [InlineData("Control", true, false, false, null)]   // modifier alone: keep waiting
    [InlineData("Escape", false, false, false, "")]      // cancels → clears
    public void A_pressed_key_becomes_upstreams_hotkey_text(string key, bool ctrl, bool shift, bool alt, string? expected)
    {
        Assert.Equal(expected, HotkeyText.FromKey(key, ctrl, shift, alt));
    }
}
