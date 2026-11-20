// aion2-overlay fork: new file (see FORK_CHANGES.md).
using System.Windows;
using AionDpsMeter.Core.Windowing;
using AionDpsMeter.UI.Pages;
using AionDpsMeter.UI.Services.Windowing;

namespace AionDpsMeter.UI.Services.TimerWindow
{
    /// <summary>The 🗺 window: the open world map as a diagram with the character's own place, pet monster spawns,
    /// gathering points, hidden cubes and field boss places.</summary>
    public sealed class MapWindow(IWindowManagerService windowManager, WindowBoundsKeeper bounds, AionDpsMeter.Timers.Feed.TimersOptionsStore options)
    {
        private BlazorWindow? window;

        /// <summary>Not in upstream's WindowKey enum, after the replay window (1014).</summary>
        public static readonly WindowKey Key = (WindowKey)1015;

        public void Open()
        {
            if (windowManager.IsOpen(Key)) { windowManager.Focus(Key); return; }
            var w = new BlazorWindow(App.AppHost.Services, typeof(MapPage))
            {
                Width = 1100,
                Height = 760,
                ShowInTaskbar = false,
                Title = "AION2 Карта",
                Topmost = options.Current.Map.OnTop,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
            };
            windowManager.Open(Key, w, isSingleton: true, persistenceMode: WindowPersistenceMode.Bounds);
            bounds.Keep(Key, w);
            window = w;
            w.Closed += (_, _) => window = null;
        }

        public void Drag() => windowManager.Drag(Key);

        /// <summary>«Поверх всех окон» from the map's own settings.</summary>
        public void SetTopmost(bool on)
        {
            if (window is { } w) w.Dispatcher.Invoke(() => w.Topmost = on);
        }

        public void Close() => windowManager.Close(Key);
    }
}
