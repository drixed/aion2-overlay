// aion2-overlay fork: new file (see FORK_CHANGES.md).
using System.Windows;
using AionDpsMeter.Core.Windowing;
using AionDpsMeter.Timers.Feed;
using AionDpsMeter.UI.Pages;
using AionDpsMeter.UI.Services.Windowing;

namespace AionDpsMeter.UI.Services.TimerWindow
{
    /// <summary>The field boss timers window: dragged by its header like upstream's main window, folds down to the
    /// header (▾), and ⚙ opens filters, notification settings and the per-row buttons.</summary>
    public sealed class TimersWindowController(IWindowManagerService windowManager, TimersOptionsStore options)
    {
        /// <summary>Deliberately not a member of upstream's WindowKey enum (no edit to their file): the window manager
        /// keys windows by value and saves bounds under key.ToString(), i.e. "1001".</summary>
        public static readonly WindowKey Key = (WindowKey)1001;

        private const double HeaderHeight = 34;
        private const double DefaultHeight = 460;

        private Window? window;

        public bool IsEditing { get; private set; }

        public event Action? EditingChanged;

        public void Open()
        {
            window = new BlazorWindow(App.AppHost.Services, typeof(TimersOverlay))
            {
                Width = 340,
                Height = DefaultHeight,
                ShowInTaskbar = false,
                Title = "AION2 Timers",
                ResizeMode = ResizeMode.CanResizeWithGrip,
                MinWidth = 220,
                MinHeight = HeaderHeight,
                // First run only: below the DPS window instead of on top of it. A saved position wins.
                WindowStartupLocation = WindowStartupLocation.Manual,
                Left = 20,
                Top = 420,
            };
            windowManager.Open(Key, window, isSingleton: true, persistenceMode: WindowPersistenceMode.Bounds);
            ApplyCollapsed();
        }

        public void ToggleEditing()
        {
            IsEditing = !IsEditing;
            EditingChanged?.Invoke();
        }

        public void Drag() => windowManager.Drag(Key);

        /// <summary>Folded, the window is only its header, so it covers no more of the game than a title bar.</summary>
        public void SetCollapsed(bool collapsed)
        {
            if (window is { } w && !options.Current.Collapsed && collapsed && w.ActualHeight > HeaderHeight + 1)
                options.Current.ExpandedHeight = w.ActualHeight;
            options.Current.Collapsed = collapsed;
            options.Save();
            ApplyCollapsed();
            EditingChanged?.Invoke();
        }

        private void ApplyCollapsed()
        {
            if (window is not { } w) return;
            if (options.Current.Collapsed)
            {
                w.ResizeMode = ResizeMode.NoResize;
                w.Height = HeaderHeight;
            }
            else
            {
                w.ResizeMode = ResizeMode.CanResizeWithGrip;
                if (w.Height <= HeaderHeight + 1)
                    w.Height = options.Current.ExpandedHeight > HeaderHeight ? options.Current.ExpandedHeight : DefaultHeight;
            }
        }
    }
}
