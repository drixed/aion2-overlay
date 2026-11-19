using AionDpsMeter.Timers.Modes;

namespace AionDpsMeter.Timers.Tests.Modes;

public class TimelineTests
{
    private static readonly DateTime T0 = new(2026, 11, 20, 21, 0, 0);
    private static DateTime At(double s) => T0.AddSeconds(s);

    [Fact]
    public void Hits_of_one_skill_close_together_are_one_cast()
    {
        var casts = Timeline.Casts([(At(0), 7), (At(0.3), 7), (At(0.6), 7), (At(1), 9), (At(5), 7)]);
        Assert.Equal([(At(0), 7), (At(1), 9), (At(5), 7)], casts);
    }

    [Fact]
    public void Uptime_merges_overlapping_buffs_and_clips_to_the_fight()
    {
        var intervals = new[] { (At(-5), At(10)), (At(8), At(20)), (At(50), At(70)) };
        var merged = Timeline.Merge(intervals, At(0), At(60));
        Assert.Equal([(At(0), At(20)), (At(50), At(60))], merged);
        Assert.Equal(0.5, Timeline.Uptime(merged, At(0), At(60)), 3);
    }

    [Fact]
    public void Share_of_casts_made_while_a_buff_was_on()
    {
        var spans = new[] { (At(0), At(10)), (At(20), At(30)) };
        Assert.Equal(0.5, Timeline.ActiveShare([At(1), At(15), At(25), At(40)], spans), 3);
        Assert.Equal(0, Timeline.ActiveShare([], spans));
    }
    [Fact]
    public void A_run_keeps_where_it_ended_and_how_many_hits_it_had()
    {
        var runs = Timeline.Runs([(At(0), 7), (At(0.3), 7), (At(0.6), 7), (At(1), 9), (At(5), 7)]);
        Assert.Equal([(At(0), At(0.6), 7, 3), (At(1), At(1), 9, 1), (At(5), At(5), 7, 1)], runs);
    }

    [Theory]
    [InlineData(18, 3)]
    [InlineData(5, 1)]
    [InlineData(47, 10)]
    [InlineData(158, 30)]
    [InlineData(900, 300)]
    public void Axis_step_gives_at_most_eight_labels(double seconds, double step)
    {
        Assert.Equal(step, Timeline.AxisStep(seconds));
    }
}
