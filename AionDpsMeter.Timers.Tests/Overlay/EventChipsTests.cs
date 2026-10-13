using AionDpsMeter.Timers.Feed;
using AionDpsMeter.Timers.Overlay;
using AionDpsMeter.Timers.Schedule;

namespace AionDpsMeter.Timers.Tests.Overlay;

public class EventChipsTests
{
    // Times from the schedule LOXIN runs on (loxin.gg, Oct 2026): air raid hourly at :30 for 3 min, Arena of Tactics
    // 11:00–14:00 and 19:00–21:00 Berlin, daily reset 07:00 UTC.
    private const string Json = """
    {
      "events": [
        { "id": "beritra-air-raid", "name": "Рейд Беритры", "kind": "interval", "anchor": "2026-10-01T00:30:00Z",
          "periodMinutes": 60, "durationMinutes": 3, "icon": "raid", "alert": false },
        { "id": "arena-of-tactics", "name": "Арена тактики 10×10", "kind": "fixed", "timeZone": "Europe/Berlin",
          "times": ["11:00", "19:00"], "durationsMinutes": [180, 120], "durationMinutes": 180, "icon": "arena", "alert": false },
        { "id": "daily-reset", "name": "Ежедневный сброс", "kind": "fixed", "timeZone": "UTC", "times": ["07:00"],
          "durationMinutes": 0, "soonMinutes": 60, "icon": "reset", "alert": false }
      ]
    }
    """;

    private static readonly ScheduleData Data = ScheduleJson.Parse(Json);

    private static readonly TimeZoneInfo Berlin = TimeZoneInfo.FindSystemTimeZoneById("W. Europe Standard Time");

    /// <summary>Tooltips show the player's local time; these tests read them in Berlin time.</summary>
    private static EventChip Chip(string id, DateTimeOffset now) => EventChips.Build(now, Data, Berlin).Single(c => c.Id == id);

    [Fact]
    public void Each_window_of_a_fixed_event_can_last_its_own_time()
    {
        var evening = Chip("arena-of-tactics", new DateTimeOffset(2026, 10, 13, 18, 30, 0, TimeSpan.Zero)); // 20:30 Berlin (CEST)
        Assert.Equal(ChipState.Open, evening.State);
        Assert.Equal("30м", evening.Text);

        var afternoon = Chip("arena-of-tactics", new DateTimeOffset(2026, 10, 13, 13, 0, 0, TimeSpan.Zero)); // 15:00 Berlin
        Assert.Equal(ChipState.Idle, afternoon.State);
        Assert.Contains("19:00", afternoon.Tooltip.Replace(' ', ' '));
    }

    [Fact]
    public void An_event_glows_shortly_before_it_opens_and_counts_down_while_open()
    {
        Assert.Equal((ChipState.Soon, "1м"), Pair(Chip("beritra-air-raid", new DateTimeOffset(2026, 10, 13, 9, 29, 0, TimeSpan.Zero))));
        Assert.Equal((ChipState.Open, "2м"), Pair(Chip("beritra-air-raid", new DateTimeOffset(2026, 10, 13, 9, 31, 0, TimeSpan.Zero))));
        Assert.Equal((ChipState.Next, "20м"), Pair(Chip("beritra-air-raid", new DateTimeOffset(2026, 10, 13, 9, 10, 0, TimeSpan.Zero)))); // the nearest one stands out
    }

    [Fact]
    public void The_daily_reset_counts_down_its_last_hour()
    {
        Assert.Equal((ChipState.Soon, "30м"), Pair(Chip("daily-reset", new DateTimeOffset(2026, 10, 13, 6, 30, 0, TimeSpan.Zero))));
        Assert.Equal(ChipState.Idle, Chip("daily-reset", new DateTimeOffset(2026, 10, 13, 5, 0, 0, TimeSpan.Zero)).State);
    }

    [Fact]
    public void With_times_always_on_a_waiting_event_shows_its_countdown_too()
    {
        var at = new DateTimeOffset(2026, 10, 13, 9, 10, 0, TimeSpan.Zero);
        Assert.Equal("20м", EventChips.Build(at, Data, Berlin, alwaysTime: true).Single(c => c.Id == "beritra-air-raid").Text);
        Assert.Equal("2ч50м", EventChips.Build(at, Data, Berlin, alwaysTime: true).Single(c => c.Id == "arena-of-tactics").Text);
    }

    /// <summary>The nearest upcoming event stands out (a pill with its name), the rest stay small.</summary>
    [Fact]
    public void The_nearest_upcoming_event_is_marked_as_next()
    {
        var at = new DateTimeOffset(2026, 10, 13, 9, 10, 0, TimeSpan.Zero); // air raid in 20 min, arena open, reset in ~21 h
        var chips = EventChips.Build(at, Data, Berlin);
        Assert.Equal(ChipState.Next, chips.Single(c => c.Id == "beritra-air-raid").State);
        Assert.Equal(ChipState.Open, chips.Single(c => c.Id == "arena-of-tactics").State);
        Assert.Equal(ChipState.Idle, chips.Single(c => c.Id == "daily-reset").State);
        Assert.Equal("20м", chips.Single(c => c.Id == "beritra-air-raid").Text); // its countdown shows without the setting
    }

    [Fact]
    public void An_event_about_to_start_is_soon_not_next()
    {
        var chips = EventChips.Build(new DateTimeOffset(2026, 10, 13, 9, 29, 0, TimeSpan.Zero), Data, Berlin);
        Assert.Equal(ChipState.Soon, chips.Single(c => c.Id == "beritra-air-raid").State);
        Assert.DoesNotContain(chips, c => c.State == ChipState.Next && c.Id == "beritra-air-raid");
    }

    [Fact]
    public void Chips_carry_the_icon_from_the_schedule() =>
        Assert.Equal("arena", Chip("arena-of-tactics", DateTimeOffset.UtcNow).Icon);

    [Fact]
    public void Events_without_an_alert_by_default_stay_quiet_until_switched_on()
    {
        var raid = TimersFeed.BuildSchedule(new DateTimeOffset(2026, 10, 13, 9, 27, 0, TimeSpan.Zero), Data).Single(i => i.Id == "beritra-air-raid");
        var options = new TimersOptions();
        Assert.False(options.Notifies(raid));
        options.SetAlert(raid.Id, on: true);
        Assert.True(options.Notifies(raid));
        options.SetAlert(raid.Id, on: false);
        Assert.False(options.Notifies(raid));
    }

    private static (ChipState, string) Pair(EventChip c) => (c.State, c.Text);
}
