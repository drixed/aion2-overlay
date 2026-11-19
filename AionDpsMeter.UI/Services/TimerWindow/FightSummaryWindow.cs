// aion2-overlay fork: new file (see FORK_CHANGES.md).
using System.Windows;
using AionDpsMeter.Core.Windowing;
using AionDpsMeter.UI.Pages;
using AionDpsMeter.UI.Services.Windowing;

namespace AionDpsMeter.UI.Services.TimerWindow
{
    /// <summary>"Итоги боя": the fight summary window, opened and closed from the main window's header.</summary>
    public sealed class FightSummaryWindow(IWindowManagerService windowManager, WindowBoundsKeeper bounds)
    {
        /// <summary>Not in upstream's WindowKey enum, like the timers window (1001) and the event panels (1004).</summary>
        public static readonly WindowKey Key = (WindowKey)1005;

        public void Toggle()
        {
            if (windowManager.IsOpen(Key))
            {
                windowManager.Close(Key);
                return;
            }
            var w = new BlazorWindow(App.AppHost.Services, typeof(FightSummaryPage))
            {
                Width = 780,
                Height = 600,
                ShowInTaskbar = false,
                Title = "AION2 Итоги боя",
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
            };
            windowManager.Open(Key, w, isSingleton: true, persistenceMode: WindowPersistenceMode.Bounds);
            bounds.Keep(Key, w);
        }

        public void Drag() => windowManager.Drag(Key);

        public void Close() => windowManager.Close(Key);
    }
}
