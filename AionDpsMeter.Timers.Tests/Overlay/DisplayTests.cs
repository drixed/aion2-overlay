using AionDpsMeter.Timers.Overlay;

namespace AionDpsMeter.Timers.Tests.Overlay;

public class DisplayTests
{
    private static readonly DateTime T0 = new(2026, 11, 20, 13, 5, 0);

    [Fact]
    public void Clock_times_follow_the_12_or_24_hour_setting()
    {
        Assert.Equal("13:05", Display.Time(T0, use12h: false));
        Assert.Equal("1:05 PM", Display.Time(T0, use12h: true));
        Assert.Equal("12:30 AM", Display.Time(new DateTime(2026, 11, 20, 0, 30, 0), use12h: true));
    }

    [Fact]
    public void Idle_fade_dims_the_window_after_the_set_time_without_a_fight()
    {
        Assert.Equal(1.0, Display.IdleOpacity(lastFight: T0, now: T0.AddSeconds(20), fadeAfterSeconds: 30, fadedPercent: 40));
        Assert.Equal(0.4, Display.IdleOpacity(T0, T0.AddSeconds(31), 30, 40), 3);
        Assert.Equal(1.0, Display.IdleOpacity(T0, T0.AddSeconds(500), 0, 40));       // off
        Assert.Equal(0.4, Display.IdleOpacity(null, T0, 30, 40), 3);                  // no fight since the start
    }
}
