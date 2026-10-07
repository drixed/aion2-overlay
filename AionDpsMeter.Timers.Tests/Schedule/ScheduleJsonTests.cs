using AionDpsMeter.Timers.Schedule;

namespace AionDpsMeter.Timers.Tests.Schedule;

public class ScheduleJsonTests
{
    [Fact]
    public void The_shipped_schedule_parses()
    {
        var data = ScheduleJson.Parse(ScheduleSource.ReadEmbedded());
        var rift = Assert.Single(data.Events, e => e.Id == "rift");
        Assert.Equal(8, rift.Times.Count);
        Assert.False(rift.Verified);
        Assert.Equal(2400, data.FieldBossMaps[1110].Block);
    }

    [Fact]
    public void A_broken_event_is_skipped_and_the_rest_kept()
    {
        var data = ScheduleJson.Parse("""
            { "events": [
                { "id": "a", "name": "A", "kind": "fixed", "timeZone": "UTC", "times": ["12:00"] },
                { "id": "b", "name": "B", "kind": "fixed", "timeZone": "UTC", "times": ["25:99"] },
                { "id": "c", "name": "C", "kind": "fixed", "timeZone": "Mars/Olympus", "times": ["12:00"] },
                { "id": "d", "name": "D", "kind": "sometimes" }
            ] }
            """);
        Assert.Equal(["a"], data.Events.Select(e => e.Id));
        Assert.True(data.Events[0].Verified); // verified defaults to true
    }

    [Fact]
    public void Interval_events_parse()
    {
        var data = ScheduleJson.Parse("""
            { "events": [ { "id": "i", "name": "I", "kind": "interval", "anchor": "2026-10-07T00:00:00Z",
                            "periodMinutes": 90, "durationMinutes": 30 } ] }
            """);
        var e = Assert.Single(data.Events);
        Assert.Equal(ScheduleKind.Interval, e.Kind);
        Assert.Equal(new DateTimeOffset(2026, 10, 7, 0, 0, 0, TimeSpan.Zero), e.Anchor);
        Assert.Equal(TimeSpan.FromMinutes(90), e.Period);
        Assert.Equal(TimeSpan.FromMinutes(30), e.Duration);
    }

    [Fact]
    public void Enrage_overrides_parse_and_zero_means_none()
    {
        var data = ScheduleJson.Parse("""{ "enrageSeconds": { "2400800": 0, "2900001": 420, "x": 5, "2900002": -1 } }""");
        Assert.Equal(0, data.Enrage[2400800]);
        Assert.Equal(420, data.Enrage[2900001]);
        Assert.Equal(2, data.Enrage.Count);
        Assert.Empty(ScheduleData.Empty.Enrage);
    }

    [Fact]
    public void A_notice_parses_and_an_incomplete_one_is_ignored()
    {
        var data = ScheduleJson.Parse("""
            { "notice": { "id": "patch-2026-10", "title": "Метр обновляется под новый патч", "text": "Вышел патч…" } }
            """);
        Assert.Equal(new Notice("patch-2026-10", "Метр обновляется под новый патч", "Вышел патч…"), data.Notice);
        Assert.Null(ScheduleJson.Parse("""{ "notice": { "id": "", "title": "x" } }""").Notice);
        Assert.Null(ScheduleData.Empty.Notice);
    }

    [Fact]
    public void Invalid_json_throws_FormatException()
    {
        Assert.Throws<FormatException>(() => ScheduleJson.Parse("{ not json"));
    }
}
