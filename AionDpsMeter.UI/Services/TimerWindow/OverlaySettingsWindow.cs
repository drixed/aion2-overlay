// aion2-overlay fork: new file (see FORK_CHANGES.md).
using AionDpsMeter.Core.Windowing;
using AionDpsMeter.UI.Pages;
using AionDpsMeter.UI.Services.Windowing;

namespace AionDpsMeter.UI.Services.TimerWindow
{
    /// <summary>The ✦ window: the fork's overlay settings (numbers, overlay behaviour, hotkeys).</summary>
    public sealed class OverlaySettingsWindow(IWindowManagerService windowManager)
    {
        /// <summary>Not in upstream's WindowKey enum on purpose, like the timers window (1001).</summary>
        public static readonly WindowKey Key = (WindowKey)1002;

        public void Open()
        {
            var window = new BlazorWindow(App.AppHost.Services, typeof(OverlaySettingsPage))
            {
                Width = 440,
                Height = 640,
                Title = "AION2 Overlay Settings",
            };
            windowManager.Open(Key, window, isSingleton: true, persistenceMode: WindowPersistenceMode.OnlyPosition);
        }

        public void Close() => windowManager.Close(Key);

        public void Drag() => windowManager.Drag(Key);
    }
}
