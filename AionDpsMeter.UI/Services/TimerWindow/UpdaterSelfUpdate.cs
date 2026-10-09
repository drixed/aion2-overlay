// aion2-overlay fork: new file (see FORK_CHANGES.md).
using System.IO;
using Microsoft.Extensions.Logging;

namespace AionDpsMeter.UI.Services.TimerWindow
{
    /// <summary>The updater cannot replace itself while it runs, so it leaves its new copy as
    /// AionDpsMeter.Updater.exe.new; the meter swaps it in on its next start, when the updater is not running.</summary>
    public static class UpdaterSelfUpdate
    {
        public static void Apply(ILogger logger)
        {
            var updater = Path.Combine(AppContext.BaseDirectory, "AionDpsMeter.Updater.exe");
            var staged = updater + ".new";
            if (!File.Exists(staged)) return;
            try
            {
                File.Move(staged, updater, overwrite: true);
                logger.LogInformation("Updater replaced with the new version");
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Updater could not be replaced; trying again on the next start");
            }
        }
    }
}
