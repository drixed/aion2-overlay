using AionDpsMeter.Timers.Feed;
using AionDpsMeter.Timers.Overlay;

namespace AionDpsMeter.Timers.Tests.Overlay;

public class TrayRulesTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 20, 3, 0, 0, TimeSpan.Zero);

    [Fact]
    public void A_closed_game_hides_the_overlay_only_when_asked()
    {
        Assert.Equal(TrayReason.GameClosed, TrayRules.Reason(new TimersOptions { TrayWhenGameClosed = true }, gameRunning: false, Now, Now, meterFocused: false));
        Assert.Equal(TrayReason.None, TrayRules.Reason(new TimersOptions(), gameRunning: false, Now, Now, meterFocused: false));
    }

    [Fact]
    public void Idle_hides_after_the_set_minutes_without_a_fight_unless_the_meter_is_in_use()
    {
        var o = new TimersOptions { HideWhenIdleMinutes = 5 };
        Assert.Equal(TrayReason.None, TrayRules.Reason(o, true, Now, Now.AddMinutes(-4), false));
        Assert.Equal(TrayReason.Idle, TrayRules.Reason(o, true, Now, Now.AddMinutes(-5), false));
        Assert.Equal(TrayReason.None, TrayRules.Reason(o, true, Now, Now.AddMinutes(-30), meterFocused: true));
        Assert.Equal(TrayReason.None, TrayRules.Reason(new TimersOptions(), true, Now, Now.AddHours(-3), false));
    }
}
