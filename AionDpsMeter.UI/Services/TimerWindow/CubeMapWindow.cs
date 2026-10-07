// aion2-overlay fork: new file (see FORK_CHANGES.md).
using System.Windows;
using System.Windows.Media;
using AionDpsMeter.Core.Windowing;
using AionDpsMeter.Timers.Schedule;
using AionDpsMeter.Timers.Zones;
using AionDpsMeter.UI.Services.Windowing;
using Microsoft.Extensions.Logging;
using Microsoft.Web.WebView2.Wpf;

namespace AionDpsMeter.UI.Services.TimerWindow
{
    /// <summary>
    /// "Карта кубов": a fan site's hidden cube map in a window over the game, opened on the map the character is on and
    /// following it to the next zone. The site is shown as a browser would show it — its data is not copied into the app
    /// (none of the cube sites licenses it for reuse); found cubes ticked on the site stay saved in this window's storage.
    /// </summary>
    public sealed class CubeMapWindow(IWindowManagerService windowManager, ZoneTracker zones, ScheduleSource schedule, WindowBoundsKeeper bounds, ILogger<CubeMapWindow> logger)
    {
        /// <summary>Not in upstream's WindowKey enum on purpose, like the timers window (1001).</summary>
        public static readonly WindowKey Key = (WindowKey)1003;

        private Window? window;
        private WebView2? view;
        private string? shownUrl;
        private bool following;

        public void Toggle()
        {
            if (windowManager.IsOpen(Key)) windowManager.Close(Key);
            else Open();
        }

        private void Open()
        {
            if (!following)
            {
                following = true;
                zones.Changed += () => Application.Current.Dispatcher.BeginInvoke(Navigate);
            }

            view = new WebView2 { DefaultBackgroundColor = System.Drawing.Color.FromArgb(255, 16, 20, 28) };
            window = new Window
            {
                Title = "Карта кубов",
                Width = 560,
                Height = 560,
                MinWidth = 300,
                MinHeight = 200,
                Topmost = true,
                ShowInTaskbar = false,
                WindowStyle = WindowStyle.ToolWindow,
                Background = new SolidColorBrush(Color.FromRgb(16, 20, 28)),
                Content = view,
            };
            window.Closed += (_, _) =>
            {
                view?.Dispose();
                view = null;
                window = null;
                shownUrl = null;
            };
            windowManager.Open(Key, window, isSingleton: true, persistenceMode: WindowPersistenceMode.Bounds);
            bounds.Keep(Key, window);
            Navigate();
        }

        private void Navigate()
        {
            if (window is null || view is null) return;
            var mapId = zones.CurrentMapId;
            var url = schedule.Current.CubeMapFor(mapId);
            window.Title = $"Карта кубов — {ZoneName(mapId)}";
            if (url is null || url == shownUrl) return;
            try
            {
                view.Source = new Uri(url);
                shownUrl = url;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Cube map {Url} could not be opened", url);
            }
        }

        private string ZoneName(int? mapId) =>
            mapId is null ? "все зоны"
            : schedule.Current.FieldBossMaps.TryGetValue(mapId.Value, out var map) && map.Name.Length > 0 ? map.Name
            : $"карта {mapId}";
    }
}
