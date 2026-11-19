// aion2-overlay fork: new file (see FORK_CHANGES.md).
using System.Windows;
using AionDpsMeter.Core.Windowing;
using AionDpsMeter.UI.Pages;
using AionDpsMeter.UI.Services.Windowing;

namespace AionDpsMeter.UI.Services.TimerWindow
{
    /// <summary>"История боёв" in the fork's design: the fights on the left, the chosen one's tabs on the right. The main
    /// window's ☰ opens it on the list, a click on a name opens it on the current fight with that player.</summary>
    public sealed class HistoryBrowserWindow(IWindowManagerService windowManager, WindowBoundsKeeper bounds)
    {
        /// <summary>Not in upstream's WindowKey enum, like the event panels (1004) and the summary (1005).</summary>
        public static readonly WindowKey Key = (WindowKey)1006;

        /// <summary>What to show when the window opens or is asked again: a player of the current fight, or the list.</summary>
        public long? RequestedPlayer { get; private set; }

        /// <summary>A saved fight to show (the records open their best fights), or null for the current one.</summary>
        public Guid? RequestedSession { get; private set; }

        public event Action? Requested;

        public void Open(long? player = null, Guid? session = null)
        {
            RequestedPlayer = player;
            RequestedSession = session;
            if (!windowManager.IsOpen(Key))
            {
                var w = new BlazorWindow(App.AppHost.Services, typeof(HistoryBrowserPage))
                {
                    Width = 1000,
                    Height = 640,
                    ShowInTaskbar = false,
                    Title = "AION2 История боёв",
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
        }

        public void Drag() => windowManager.Drag(Key);

        public void Close() => windowManager.Close(Key);
    }
}
