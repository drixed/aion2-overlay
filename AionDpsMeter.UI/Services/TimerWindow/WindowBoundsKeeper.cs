// aion2-overlay fork: new file (see FORK_CHANGES.md).
using System.Windows;
using System.Windows.Threading;
using AionDpsMeter.Core.Windowing;
using AionDpsMeter.Services.Services.Settings;

namespace AionDpsMeter.UI.Services.TimerWindow
{
    /// <summary>
    /// Saves where a window is and how big it is a second after it was moved or resized. Upstream saves the main window
    /// only in OnClosed (skipped when the app ends another way, e.g. for an update) and other windows only after a
    /// header drag, so a restart lost the place and every resize.
    /// </summary>
    public sealed class WindowBoundsKeeper(IAppSettingsService settings)
    {
        private static readonly TimeSpan Quiet = TimeSpan.FromSeconds(1);

        /// <summary>The main window: upstream's own WindowLeft/Top/Width/Height, which it restores at start.</summary>
        public void KeepMain(Window window) => Keep(window, () =>
        {
            settings.WindowLeft = window.Left;
            settings.WindowTop = window.Top;
            settings.WindowWidth = window.Width;
            settings.WindowHeight = window.Height;
        });

        /// <summary>A window opened through the window manager with <see cref="WindowPersistenceMode.Bounds"/>.</summary>
        public void Keep(WindowKey key, Window window) => Keep(window, () => settings.SetWindowBounds(key, new WindowBounds
        {
            Left = window.Left,
            Top = window.Top,
            Width = window.Width,
            Height = window.Height,
        }));

        private static void Keep(Window window, Action save)
        {
            var timer = new DispatcherTimer { Interval = Quiet };
            void SaveNow()
            {
                timer.Stop();
                // Minimized windows sit at -32000; hidden ones keep their last real place anyway.
                if (window.WindowState == WindowState.Normal && !double.IsNaN(window.Left)) save();
            }
            void Changed()
            {
                timer.Stop();
                timer.Start();
            }
            timer.Tick += (_, _) => SaveNow();
            window.LocationChanged += (_, _) => Changed();
            window.SizeChanged += (_, _) => Changed();
            window.Closing += (_, _) => { if (timer.IsEnabled) SaveNow(); };
        }
    }
}
