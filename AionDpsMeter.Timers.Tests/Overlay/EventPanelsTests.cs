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
}
