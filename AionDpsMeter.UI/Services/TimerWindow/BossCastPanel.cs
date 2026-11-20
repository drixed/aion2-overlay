// aion2-overlay fork: new file (see FORK_CHANGES.md).
using System.Windows;
using System.Windows.Threading;
using AionDpsMeter.Core.Windowing;
using AionDpsMeter.Timers.Bosses;
using AionDpsMeter.Timers.Feed;
using AionDpsMeter.UI.Pages;
using AionDpsMeter.UI.Services.Windowing;

namespace AionDpsMeter.UI.Services.TimerWindow
{
    /// <summary>
    /// The boss cast overlay: every skill a boss winds up, with the time left, its target and whether it could kill me. Shown while a cast
    /// runs; clicks go through to the game, except while "Расставить" is on.
    /// </summary>
    public sealed class BossCastPanel(IWindowManagerService windowManager, TimersOptionsStore options, BossCastTracker casts)
    {
        /// <summary>Not in upstream's WindowKey enum, after the craft overlay (1017).</summary>
        public static readonly WindowKey Key = (WindowKey)1018;

        private Window? window;
        private DispatcherTimer? timer;
        private bool started;

        public bool Editing { get; private set; }

        public event Action? Changed;

        public void Start()
        {
            if (started) return;
            started = true;
            timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
            timer.Tick += (_, _) => Tick();
            timer.Start();
        }

        public void ToggleEditing()
        {
            Editing = !Editing;
            Tick();
            if (window is not null)
            {
                if (Editing) windowManager.RestoreClickThrough(Key);
                else windowManager.SetClickThrough(Key);
            }
            Changed?.Invoke();
        }

        public void ResetPlace()
        {
            options.Current.BossCastsLeft = null;
            options.Current.BossCastsTop = null;
            options.Save();
            if (window is not null) { window.Left = 560; window.Top = 120; }
        }

        public void Drag()
        {
            if (!Editing || window is null) return;
            windowManager.Drag(Key);
            options.Current.BossCastsLeft = window.Left;
            options.Current.BossCastsTop = window.Top;
            options.Save();
        }

        private void Tick()
        {
            var show = options.Current.BossCasts && (Editing || casts.Active(DateTime.Now).Count > 0);
            if (show) Show();
            else if (window is { IsVisible: true }) window.Hide();
        }

        private void Show()
        {
            if (window is null)
            {
                var o = options.Current;
                window = new BlazorWindow(App.AppHost.Services, typeof(BossCastPage))
                {
                    ShowInTaskbar = false,
                    Title = "AION2 Касты босса",
                    WindowStartupLocation = WindowStartupLocation.Manual,
                    Left = o.BossCastsLeft ?? 560,
                    Top = o.BossCastsTop ?? 120,
                    Width = 380,
                    Height = 210,
                };
                windowManager.Open(Key, window, isSingleton: true);
                if (!Editing) windowManager.SetClickThrough(Key);
            }
            if (!window.IsVisible) window.Show();
        }
    }
}
