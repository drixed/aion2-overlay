using AionDpsMeter.Timers.Buffs;

namespace AionDpsMeter.Timers.Tests.Buffs;

public class BuffTrackerTests
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 20, 3, 0, 0, TimeSpan.Zero);
    private static readonly BuffPreset Preset = new() { Name = "Бой", ClassId = 15, BuffIds = [101, 202, 303] };

    [Fact]
    public void Tiles_follow_the_preset_with_time_left_and_missing_ones()
    {
        var t = new BuffTracker();
        t.OnBuff(202, "Щит", "s.png", 60_000, T0);
        t.OnBuff(101, "Ярость", null, 30_000, T0);

        var tiles = t.Tiles(Preset, T0.AddSeconds(10));
        Assert.Equal([101, 202, 303], tiles.Select(x => x.BuffId));
        Assert.Equal(TimeSpan.FromSeconds(20), tiles[0].Left);
        Assert.Equal(TimeSpan.FromSeconds(50), tiles[1].Left);
        Assert.Null(tiles[2].Left);                       // never seen: missing
        Assert.Equal("Ярость", tiles[0].Name);
        Assert.Equal(2 / 3.0, tiles[0].Share, 3);
    }

    [Fact]
    public void The_panel_shows_only_the_buffs_that_are_on_in_the_preset_order()
    {
        var t = new BuffTracker();
        t.OnBuff(303, "Ярость", null, 30_000, T0);
        t.OnBuff(101, "Щит", null, 5_000, T0);
        t.OnBuff(999, "Чужой", null, 60_000, T0); // not in the preset
        Assert.Equal([101, 303], t.ActiveTiles(Preset, T0.AddSeconds(2)).Select(x => x.BuffId));
        Assert.Equal([303], t.ActiveTiles(Preset, T0.AddSeconds(6)).Select(x => x.BuffId)); // the shield ran out: gone
        Assert.Empty(t.ActiveTiles(Preset, T0.AddSeconds(31)));
    }

    [Fact]
    public void Switching_to_another_character_drops_the_old_ones_buffs_but_keeps_the_seen_list()
    {
        var t = new BuffTracker();
        Assert.False(t.ForCharacter("DRIXED·15"));        // the first character: nothing to drop
        t.OnBuff(101, "Ярость", null, 600_000, T0);
        Assert.False(t.ForCharacter("DRIXED·15"));        // the same one again
        Assert.Single(t.ActiveTiles(Preset, T0.AddSeconds(1)));

        Assert.True(t.ForCharacter("Alt·11"));            // another character: the old buffs go
        Assert.Empty(t.ActiveTiles(Preset, T0.AddSeconds(1)));
        Assert.Empty(t.Expired(Preset, T0.AddMinutes(20))); // and no "спал" for them
        Assert.Contains(t.Seen(), s => s.Id == 101);
    }

    [Fact]
    public void A_refreshed_buff_runs_from_the_new_cast()
    {
        var t = new BuffTracker();
        t.OnBuff(101, "Ярость", null, 30_000, T0);
        t.OnBuff(101, "Ярость", null, 30_000, T0.AddSeconds(20));
        Assert.Equal(TimeSpan.FromSeconds(25), t.Tiles(Preset, T0.AddSeconds(25))[0].Left);
    }

    [Fact]
    public void A_tracked_buff_running_out_is_reported_once()
    {
        var t = new BuffTracker();
        t.OnBuff(101, "Ярость", null, 30_000, T0);
        t.OnBuff(999, "Чужой", null, 1_000, T0);           // not in the preset
        Assert.Empty(t.Expired(Preset, T0.AddSeconds(10)));
        Assert.Equal([101], t.Expired(Preset, T0.AddSeconds(31)).Select(x => x.Id));
        Assert.Empty(t.Expired(Preset, T0.AddSeconds(40)));
        t.OnBuff(101, "Ярость", null, 30_000, T0.AddSeconds(50));
        Assert.Equal([101], t.Expired(Preset, T0.AddSeconds(81)).Select(x => x.Id));
    }

    [Fact]
    public void Buffs_seen_on_the_player_are_remembered_for_the_picker()
    {
        var t = new BuffTracker();
        t.OnBuff(101, "Ярость", "r.png", 30_000, T0);
        t.OnBuff(101, "Ярость", "r.png", 30_000, T0.AddSeconds(5));
        t.OnBuff(202, "Щит", null, 30_000, T0.AddSeconds(6));
        Assert.Equal([202, 101], t.Seen().Select(s => s.Id)); // the last seen first, once each
    }

    [Theory]
    [InlineData("bosses", false, false, false)]
    [InlineData("bosses", true, false, false)]
    [InlineData("bosses", true, true, true)]
    [InlineData("fights", true, false, true)]
    [InlineData("fights", false, false, false)]
    [InlineData("always", false, false, true)]
    public void Where_the_trackers_show(string mode, bool inFight, bool bossOrDummy, bool shown)
    {
        Assert.Equal(shown, BuffTracker.Shows(mode, inFight, bossOrDummy));
    }
}
