// aion2-overlay fork: new file (see FORK_CHANGES.md).
using System.Diagnostics;
using System.Windows;
using AionDpsMeter.Core.Windowing;
using AionDpsMeter.UI.Pages;
using AionDpsMeter.UI.Services.Windowing;

namespace AionDpsMeter.UI.Services.TimerWindow
{
    /// <summary>The 📣 window: NCSOFT's announcements and Twitch Drops campaigns.</summary>
    public sealed class NewsWindow(IWindowManagerService windowManager, WindowBoundsKeeper bounds, NewsService news)
    {
        /// <summary>Not in upstream's WindowKey enum, after the stat calculator (1010).</summary>
        public static readonly WindowKey Key = (WindowKey)1011;

        public string Tab { get; private set; } = "news";

        public event Action? Requested;

        public void Open() => Open("news");

        public void Open(string tab)
        {
            Tab = tab;
            if (!windowManager.IsOpen(Key))
            {
                var w = new BlazorWindow(App.AppHost.Services, typeof(NewsPage))
                {
                    Width = 720,
                    Height = 640,
                    ShowInTaskbar = false,
                    Title = "AION2 Объявления",
                    WindowStartupLocation = WindowStartupLocation.CenterScreen,
                };
                windowManager.Open(Key, w, isSingleton: true, persistenceMode: WindowPersistenceMode.Bounds);
                bounds.Keep(Key, w);
            }
            else
            {
                windowManager.Focus(Key);
            }
            Requested?.Invoke();
            _ = news.RefreshAsync();
        }

        public void Drag() => windowManager.Drag(Key);

        public void Close() => windowManager.Close(Key);

        /// <summary>Opens a page in the user's browser (NC's notice, Twitch); only our own fixed or NC-given https links.</summary>
        public static void Browse(string url)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps) return;
            try { Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true }); }
            catch (Exception) { }
        }
    }
}
