// aion2-overlay fork: new file (see FORK_CHANGES.md).
using System.Windows;
using AionDpsMeter.Core.Windowing;
using AionDpsMeter.UI.Pages;
using AionDpsMeter.UI.Services.Windowing;

namespace AionDpsMeter.UI.Services.TimerWindow
{
    /// <summary>The "Крафт" window: recipes to make, what they need and what is still missing (inventory and both storages).</summary>
    public sealed class CraftWindow(IWindowManagerService windowManager, WindowBoundsKeeper bounds, AionDpsMeter.Timers.Feed.TimersOptionsStore options)
    {
        /// <summary>The small "Ещё нужно" overlay, after the craft window (1016).</summary>
        public static readonly WindowKey OverlayKey = (WindowKey)1017;

        private BlazorWindow? overlay;

        public bool OverlayOpen => overlay is not null;

        public event Action? OverlayChanged;

        /// <summary>At the meter's start: the overlay comes back when it was open.</summary>
        public void Start()
        {
            if (options.Current.CraftOverlay) ShowOverlay();
        }

        public void ToggleOverlay()
        {
            if (overlay is null) ShowOverlay();
            else CloseOverlay();
        }

        public void ShowOverlay()
        {
            if (overlay is not null) { windowManager.Focus(OverlayKey); return; }
            var o = options.Current;
            overlay = new BlazorWindow(App.AppHost.Services, typeof(CraftOverlayPage))
            {
                Width = 290,
                Height = 230,
                ShowInTaskbar = false,
                Title = "AION2 Ещё нужно",
                WindowStartupLocation = WindowStartupLocation.Manual,
                Left = o.CraftOverlayLeft ?? 40,
                Top = o.CraftOverlayTop ?? 260,
            };
            overlay.Closed += (_, _) => { overlay = null; OverlayChanged?.Invoke(); };
            windowManager.Open(OverlayKey, overlay, isSingleton: true);
            if (!o.CraftOverlay) { o.CraftOverlay = true; options.Save(); }
            OverlayChanged?.Invoke();
        }

        public void CloseOverlay()
        {
            options.Current.CraftOverlay = false;
            options.Save();
            windowManager.Close(OverlayKey);
        }

        public void DragOverlay()
        {
            if (overlay is null) return;
            windowManager.Drag(OverlayKey);
            options.Current.CraftOverlayLeft = overlay.Left;
            options.Current.CraftOverlayTop = overlay.Top;
            options.Save();
        }

        /// <summary>Not in upstream's WindowKey enum, after the map (1015).</summary>
        public static readonly WindowKey Key = (WindowKey)1016;

        public void Open()
        {
            if (windowManager.IsOpen(Key)) { windowManager.Focus(Key); return; }
            var w = new BlazorWindow(App.AppHost.Services, typeof(CraftPage))
            {
                Width = 1000,
                Height = 720,
                ShowInTaskbar = false,
                Title = "AION2 Крафт",
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
            };
            windowManager.Open(Key, w, isSingleton: true, persistenceMode: WindowPersistenceMode.Bounds);
            bounds.Keep(Key, w);
        }

        public void Drag() => windowManager.Drag(Key);

        public void Close() => windowManager.Close(Key);
    }
}
