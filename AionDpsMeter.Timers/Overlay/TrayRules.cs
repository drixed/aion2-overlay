// aion2-overlay fork: new file (see FORK_CHANGES.md).
using AionDpsMeter.Timers.Feed;

namespace AionDpsMeter.Timers.Overlay;

public enum TrayReason { None, GameClosed, Idle }

/// <summary>When the overlay goes to the tray by itself: the game closed ("В трей при закрытии игры") or no fight for the
/// set minutes ("Скрывать в простое") while the meter is not in use.</summary>
public static class TrayRules
{
    public static TrayReason Reason(TimersOptions o, bool gameRunning, DateTimeOffset now, DateTimeOffset lastFight, bool meterFocused)
    {
        if (o.TrayWhenGameClosed && !gameRunning) return TrayReason.GameClosed;
        if (o.HideWhenIdleMinutes > 0 && !meterFocused && now - lastFight >= TimeSpan.FromMinutes(o.HideWhenIdleMinutes)) return TrayReason.Idle;
        return TrayReason.None;
    }
}
