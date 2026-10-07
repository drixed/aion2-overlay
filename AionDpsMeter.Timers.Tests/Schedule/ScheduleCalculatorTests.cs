using System.Globalization;
using AionDpsMeter.Timers.Schedule;

namespace AionDpsMeter.Timers.Tests.Schedule;

public class ScheduleCalculatorTests
{
    private static readonly TimeZoneInfo Berlin = TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");
    private static readonly string[] EveryThreeHours = ["02:00", "05:00", "08:00", "11:00", "14:00", "17:00", "20:00", "23:00"];

    private static ScheduledEvent Fixed(string[] times, int durationMinutes = 0, DayOfWeek[]? days = null) =>
        new("e", "E", "event", ScheduleKind.Fixed, Berlin,
            times.Select(t => TimeOnly.Parse(t, CultureInfo.InvariantCulture)).ToList(),
            days?.ToHashSet(), null, null, TimeSpan.FromMinutes(durationMinutes), true);

    private static ScheduledEvent Interval(DateTimeOffset anchor, int periodMinutes, int durationMinutes = 0) =>
        new("i", "I", "event", ScheduleKind.Interval, TimeZoneInfo.Utc, [], null,
            anchor, TimeSpan.FromMinutes(periodMinutes), TimeSpan.FromMinutes(durationMinutes), true);

    private static DateTimeOffset InBerlin(int y, int mo, int d, int h, int mi)
    {
        var local = new DateTime(y, mo, d, h, mi, 0);
        return new DateTimeOffset(local, Berlin.GetUtcOffset(local));
    }

    private static DateTimeOffset Utc(int d, int h, int mi) => new(2026, 10, d, h, mi, 0, TimeSpan.Zero);

    [Fact]
    public void Fixed_returns_next_time_today()
    {
        var o = ScheduleCalculator.Next(Fixed(EveryThreeHours), InBerlin(2026, 10, 7, 10, 30))!.Value;
        Assert.Equal(InBerlin(2026, 10, 7, 11, 0), o.Start);
        Assert.Equal(o.Start, o.End);
    }

    [Fact]
    public void Fixed_returns_the_occurrence_running_now()
    {
        var now = InBerlin(2026, 10, 7, 11, 20);
        var o = ScheduleCalculator.Next(Fixed(EveryThreeHours, durationMinutes: 60), now)!.Value;
        Assert.Equal(InBerlin(2026, 10, 7, 11, 0), o.Start);
        Assert.True(o.IsActive(now));
    }

    [Fact]
    public void Fixed_wraps_past_midnight()
    {
        var o = ScheduleCalculator.Next(Fixed(["23:00"]), InBerlin(2026, 10, 7, 23, 30))!.Value;
        Assert.Equal(InBerlin(2026, 10, 8, 23, 0), o.Start);
    }

    [Fact]
    public void Fixed_respects_days_of_week()
    {
        // 2026-10-07 is a Wednesday; the next Saturday is 2026-10-10.
        var o = ScheduleCalculator.Next(Fixed(["20:00"], days: [DayOfWeek.Saturday]), InBerlin(2026, 10, 7, 21, 0))!.Value;
        Assert.Equal(InBerlin(2026, 10, 10, 20, 0), o.Start);
    }

    [Fact]
    public void Fixed_survives_the_spring_dst_gap()
    {
        // 2026-03-29 02:00 → 03:00 in Berlin: 02:30 does not exist and moves to 03:30 CEST (01:30 UTC).
        var o = ScheduleCalculator.Next(Fixed(["02:30"]), InBerlin(2026, 3, 29, 0, 0))!.Value;
        Assert.Equal(new DateTimeOffset(2026, 3, 29, 1, 30, 0, TimeSpan.Zero), o.Start);
    }

    [Fact]
    public void The_users_own_offset_does_not_change_the_answer()
    {
        var berlin = InBerlin(2026, 10, 7, 10, 30);
        var tokyo = berlin.ToOffset(TimeSpan.FromHours(9));
        Assert.Equal(ScheduleCalculator.Next(Fixed(EveryThreeHours), berlin)!.Value.Start,
                     ScheduleCalculator.Next(Fixed(EveryThreeHours), tokyo)!.Value.Start);
    }

    [Fact]
    public void Interval_steps_from_the_anchor()
    {
        var e = Interval(Utc(7, 0, 0), periodMinutes: 90);
        Assert.Equal(Utc(7, 1, 30), ScheduleCalculator.Next(e, Utc(7, 1, 0))!.Value.Start);
        Assert.Equal(Utc(7, 3, 0), ScheduleCalculator.Next(e, Utc(7, 1, 30))!.Value.Start);
        Assert.Equal(Utc(7, 0, 0), ScheduleCalculator.Next(e, Utc(6, 22, 0))!.Value.Start);
    }

    [Fact]
    public void Interval_returns_the_occurrence_running_now()
    {
        var now = Utc(7, 1, 40);
        var o = ScheduleCalculator.Next(Interval(Utc(7, 0, 0), 90, durationMinutes: 30), now)!.Value;
        Assert.Equal(Utc(7, 1, 30), o.Start);
        Assert.True(o.IsActive(now));
    }
}
