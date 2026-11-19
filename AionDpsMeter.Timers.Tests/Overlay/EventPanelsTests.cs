using AionDpsMeter.Timers.Overlay;
using AionDpsMeter.Timers.Schedule;

namespace AionDpsMeter.Timers.Tests.Overlay;

public class EventPanelsTests
{
    private const string Json = """
    {
      "events": [
        { "id": "shugo-festival", "name": "Фестиваль шуго", "kind": "interval", "anchor": "2026-11-01T00:15:00Z",
          "periodMinutes": 60, "durationMinutes": 10, "icon": "shugo" },
        { "id": "rift", "name": "Пространственный разлом", "kind": "interval", "anchor": "2026-11-01T02:00:00Z",
          "periodMinutes": 180, "durationMinutes": 60, "icon": "rift" },
        { "id": "daily-reset", "name": "Ежедневный сброс", "kind": "fixed", "timeZone": "UTC", "times": ["07:00"],
          "durationMinutes": 0, "icon": "reset" }
      ]
    }
    """;

    private static readonly ScheduleData Data = ScheduleJson.Parse(Json);
    private static readonly DateTimeOffset At = new(2026, 11, 20, 0, 0, 1, TimeSpan.Zero);

    [Fact]
    public void Tiles_follow_the_panel_s_order_and_skip_events_the_schedule_no_longer_has()
    {
        var panel = new EventPanel { EventIds = ["rift", "gone", "shugo-festival"] };
        var tiles = EventPanels.Tiles(At, Data, panel);
        Assert.Equal(["rift", "shugo-festival"], tiles.Select(t => t.Id));
    }

    [Fact]
    public void A_waiting_event_counts_down_to_its_start_like_the_game_clock()
    {
        var tiles = EventPanels.Tiles(At, Data, new EventPanel { EventIds = ["shugo-festival", "rift"] });
        Assert.Equal(new EventTile("shugo-festival", "Фестиваль шуго", "shugo", ChipState.Idle, "14:59", 0), tiles[0]);
        Assert.Equal("1:59:59", tiles[1].Countdown);                // the next rift at 02:00
    }

    [Fact]
    public void An_open_event_counts_down_to_its_end_and_fills_up()
    {
        var tile = EventPanels.Tiles(new DateTimeOffset(2026, 11, 20, 0, 20, 0, TimeSpan.Zero), Data, new EventPanel { EventIds = ["shugo-festival"] }).Single();
        Assert.Equal(ChipState.Open, tile.State);
        Assert.Equal("5:00", tile.Countdown);
        Assert.Equal(0.5, tile.Progress, 3);
    }

    [Fact]
    public void Shortly_before_the_start_the_tile_glows()
    {
        var tile = EventPanels.Tiles(new DateTimeOffset(2026, 11, 20, 0, 13, 30, TimeSpan.Zero), Data, new EventPanel { EventIds = ["shugo-festival"] }).Single();
        Assert.Equal(ChipState.Soon, tile.State);
        Assert.Equal("1:30", tile.Countdown);
    }

    [Fact]
    public void A_new_panel_gets_its_own_id_and_the_usual_look()
    {
        var a = EventPanel.Create("Панель 1", ["shugo-festival"]);
        var b = EventPanel.Create("Панель 2", []);
        Assert.NotEqual(a.Id, b.Id);
        Assert.Equal(("cards", "square", 36, 4, 100), (a.Look, a.Shape, a.IconSize, a.Spacing, a.Opacity));
        Assert.True(a.Visible);
    }

    [Fact]
    public void A_bigger_text_makes_a_bigger_window()
    {
        var small = new EventPanel { EventIds = ["a", "b", "c"], TextSize = 11 };
        var big = new EventPanel { EventIds = ["a", "b", "c"], TextSize = 16 };
        var (w1, h1) = EventPanels.WindowSize(small);
        var (w2, h2) = EventPanels.WindowSize(big);
        Assert.True(w2 > w1 && h2 > h1);
    }

    [Fact]
    public void Vertical_stacks_the_tiles_and_horizontal_lines_them_up()
    {
        var row = EventPanels.WindowSize(new EventPanel { EventIds = ["a", "b", "c"] });
        var column = EventPanels.WindowSize(new EventPanel { EventIds = ["a", "b", "c"], Vertical = true });
        Assert.True(row.Width > row.Height);
        Assert.True(column.Height > column.Width);
    }

    [Fact]
    public void A_new_panel_uses_the_system_font_at_11px() =>
        Assert.Equal(("system", 11), (EventPanel.Create("x", []).Font, EventPanel.Create("x", []).TextSize));

    private static AionDpsMeter.Timers.Bosses.BossTimer Boss(int key, bool alive, DateTimeOffset? next) =>
        new(1, key, 100, 1, alive, alive ? At.AddMinutes(-3) : null, next, null, At);

    [Fact]
    public void A_field_boss_counts_down_to_its_respawn_and_shows_when_it_is_up()
    {
        var panel = new EventPanel { EventIds = ["boss:7", "boss:8", "boss:9", "boss:10"] };
        var bosses = new[] { Boss(7, false, At.AddMinutes(30)), Boss(8, true, null), Boss(9, false, At.AddMinutes(-2)), Boss(10, false, null) };
        var tiles = EventPanels.Tiles(At, Data, panel, bosses, key => $"Босс {key}");
        Assert.Equal(new EventTile("boss:7", "Босс 7", "boss", ChipState.Idle, "30:00", 0), tiles[0]);
        Assert.Equal((ChipState.Open, "жив"), (tiles[1].State, tiles[1].Countdown));
        Assert.Equal((ChipState.Soon, "скоро"), (tiles[2].State, tiles[2].Countdown));   // respawn time passed, no news yet
        Assert.Equal((ChipState.Idle, "—"), (tiles[3].State, tiles[3].Countdown));        // never seen on the map
    }

    [Fact]
    public void A_boss_the_meter_does_not_know_on_this_server_is_left_out() =>
        Assert.Empty(EventPanels.Tiles(At, Data, new EventPanel { EventIds = ["boss:99"] }, [], k => "x"));
    // Shugo festival: entry opens on the hour, the festival itself starts 3 minutes later (game: "02:28 Войти").
    private static readonly ScheduleData WithEntry = ScheduleJson.Parse("""
    { "events": [ { "id": "shugo-festival", "name": "Фестиваль шуго", "kind": "interval", "anchor": "2026-11-01T00:00:00Z",
      "periodMinutes": 60, "durationMinutes": 10, "entryMinutes": 3, "icon": "shugo" } ] }
    """);

    [Fact]
    public void While_only_the_entry_is_open_the_tile_glows_and_counts_down_to_the_start()
    {
        var panel = new EventPanel { EventIds = ["shugo-festival"] };
        var entry = EventPanels.Tiles(new DateTimeOffset(2026, 11, 20, 22, 0, 32, TimeSpan.Zero), WithEntry, panel).Single();
        Assert.Equal(ChipState.Soon, entry.State);
        Assert.Equal("вход 2:28", entry.Countdown);

        var running = EventPanels.Tiles(new DateTimeOffset(2026, 11, 20, 22, 4, 0, TimeSpan.Zero), WithEntry, panel).Single();
        Assert.Equal(ChipState.Open, running.State);
        Assert.Equal("6:00", running.Countdown);
    }
    [Fact]
    public void An_unnamed_map_place_on_a_panel_shows_its_countdown_and_survives_being_named()
    {
        var spawn = At.AddMinutes(10);
        var panel = new EventPanel { EventIds = ["boss:-101016"] };
        var unnamed = new AionDpsMeter.Timers.Bosses.BossTimer(1, -101016, 1010, 101016, false, null, spawn, null, null);
        var tile = EventPanels.Tiles(At, Data, panel, [unnamed], k => "Глава армии Рагт").Single();
        Assert.Equal("Место 16", tile.Name);
        Assert.Equal("10:00", tile.Countdown);

        var named = unnamed with { Key = 2101131 };
        var after = EventPanels.Tiles(At, Data, panel, [named], k => k == 2101131 ? "Глава армии Рагт" : "?").Single();
        Assert.Equal("Глава армии Рагт", after.Name);
        Assert.Equal("10:00", after.Countdown);
    }
}
