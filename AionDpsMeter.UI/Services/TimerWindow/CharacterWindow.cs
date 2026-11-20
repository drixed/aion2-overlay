// aion2-overlay fork: new file (see FORK_CHANGES.md).
using System.Windows;
using AionDpsMeter.Core.Windowing;
using AionDpsMeter.UI.Pages;
using AionDpsMeter.UI.Services.Windowing;

namespace AionDpsMeter.UI.Services.TimerWindow
{
    /// <summary>The 👤 window: the played character's equipment and arcana as the game sends them at login.</summary>
    public sealed class CharacterWindow(IWindowManagerService windowManager, WindowBoundsKeeper bounds)
    {
        /// <summary>Not in upstream's WindowKey enum, after the buff panel (1012).</summary>
        public static readonly WindowKey Key = (WindowKey)1013;

        public void Open()
        {
            if (windowManager.IsOpen(Key)) { windowManager.Focus(Key); return; }
            var w = new BlazorWindow(App.AppHost.Services, typeof(CharacterPage))
            {
                Width = 900,
                Height = 720,
                ShowInTaskbar = false,
                Title = "AION2 Персонаж",
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
            };
            windowManager.Open(Key, w, isSingleton: true, persistenceMode: WindowPersistenceMode.Bounds);
            bounds.Keep(Key, w);
        }

        public void Drag() => windowManager.Drag(Key);

        public void Close() => windowManager.Close(Key);
    }
}
