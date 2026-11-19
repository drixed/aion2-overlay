// aion2-overlay fork: new file (see FORK_CHANGES.md).
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace AionDpsMeter.UI.Services.TimerWindow
{
    /// <summary>Process-wide bits the settings drive: the DPS list's refresh rate, starting with Windows, and whether the
    /// game is running (for "без звука, когда игра закрыта").</summary>
    public static class ForkRuntime
    {
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string RunValue = "AION2Overlay";

        /// <summary>Read by upstream's refresh loop after every tick.</summary>
        public static TimeSpan RefreshInterval { get; set; } = TimeSpan.FromMilliseconds(33);

        public static bool GameRunning()
        {
            try { return Process.GetProcessesByName("aion2").Length > 0 || Process.GetProcessesByName("AION2").Length > 0; }
            catch (InvalidOperationException) { return true; }
        }

        /// <summary>Writes or removes the meter's entry in the user's Run key; a missing or changed path is rewritten.</summary>
        public static void ApplyAutostart(bool on, ILogger logger)
        {
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(RunKey, writable: true);
                var current = key.GetValue(RunValue) as string;
                if (on)
                {
                    var command = $"\"{Environment.ProcessPath}\"";
                    if (current != command) key.SetValue(RunValue, command);
                }
                else if (current is not null)
                {
                    key.DeleteValue(RunValue, throwOnMissingValue: false);
                }
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or System.IO.IOException)
            {
                logger.LogWarning(ex, "Autostart could not be changed");
            }
        }
    }
}
