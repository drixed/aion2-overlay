// aion2-overlay fork: new file (see FORK_CHANGES.md).
using System.Globalization;

namespace AionDpsMeter.Timers.Overlay;

/// <summary>Small display rules from the settings: 12/24-hour clock times and the idle fade of the main window.</summary>
public static class Display
{
    /// <summary>"13:05" or "1:05 PM".</summary>
    public static string Time(DateTime local, bool use12h) =>
        use12h ? local.ToString("h:mm tt", CultureInfo.InvariantCulture) : local.ToString("HH:mm", CultureInfo.InvariantCulture);

    /// <summary>The window's opacity factor: full during and shortly after a fight, <paramref name="fadedPercent"/> once
    /// <paramref name="fadeAfterSeconds"/> passed without one (0 seconds: never fades).</summary>
    public static double IdleOpacity(DateTime? lastFight, DateTime now, int fadeAfterSeconds, int fadedPercent)
    {
        if (fadeAfterSeconds <= 0) return 1.0;
        var idle = lastFight is not { } last || (now - last).TotalSeconds > fadeAfterSeconds;
        return idle ? Math.Clamp(fadedPercent, 10, 100) / 100.0 : 1.0;
    }
}
