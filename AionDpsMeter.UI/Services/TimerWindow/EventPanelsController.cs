// aion2-overlay fork: new file (see FORK_CHANGES.md).
using System.Windows;
using System.Windows.Threading;
using AionDpsMeter.Core.Windowing;
using AionDpsMeter.Services.Services.Session;
using AionDpsMeter.Timers.Feed;
using AionDpsMeter.Timers.Overlay;
using AionDpsMeter.UI.Pages;
using AionDpsMeter.UI.Services.Windowing;

namespace AionDpsMeter.UI.Services.TimerWindow
{
    /// <summary>
    /// The floating event panels: one transparent window per visible panel, placed where the user put it. Locked they
    /// let clicks through to the game; "Расставить панели" makes them draggable. "Вне боя" hides them during a fight.
    /// </summary>
    public sealed class EventPanelsController(IWindowManagerService windowManager, TimersOptionsStore options, CombatSessionManager sessions)
    {
        /// <summary>Not in upstream's WindowKey enum, like the timers window (1001) and the cube map (1003).</summary>
        public static readonly WindowKey Key = (WindowKey)1004;

        private readonly Dictionary<string, Window> windows = [];
        private DispatcherTimer? combatWatch;
        private bool started;

        /// <summary>Panels are draggable and show their outline and name.</summary>
        public bool Editing { get; private set; }

        public event Action? EditingChanged;

        public void Start()
        {
            if (started) return;
            started = true;
            options.Changed += () => Application.Current.Dispatcher.BeginInvoke(Sync);
            combatWatch = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            combatWatch.Tick += (_, _) => ApplyVisibility();
            combatWatch.Start();
            Sync();
        }

        public void ToggleEditing()
        {
            Editing = !Editing;
            foreach (var (id, w) in windows) ApplyClickThrough(id, w);
            ApplyVisibility();
            EditingChanged?.Invoke();
        }

        /// <summary>From the panel page while placing: the window follows the mouse, then its position is kept.</summary>
        public void Drag(string panelId)
        {
            if (!Editing || !windows.TryGetValue(panelId, out var w)) return;
            windowManager.Drag(Key, panelId);
            if (options.Current.EventPanels.FirstOrDefault(p => p.Id == panelId) is { } panel
                && (panel.Left != w.Left || panel.Top != w.Top))
            {
                panel.Left = w.Left;
                panel.Top = w.Top;
                options.Save();
            }
        }

        /// <summary>Opens, resizes and closes windows to match the panels in the settings.</summary>
        private void Sync()
        {
            var wanted = options.Current.EventPanels.Where(p => p.Visible).ToDictionary(p => p.Id);
            foreach (var id in windows.Keys.Where(id => !wanted.ContainsKey(id)).ToList())
            {
                windowManager.Close(Key, id);
                windows.Remove(id);
            }
            foreach (var panel in wanted.Values)
            {
                if (!windows.TryGetValue(panel.Id, out var w)) w = Open(panel);
                var (width, height) = EventPanels.WindowSize(panel);
                w.Width = width;
                w.Height = height;
                w.Opacity = Math.Clamp(panel.Opacity, 20, 100) / 100.0;
                if (panel.Left is { } left && panel.Top is { } top && (w.Left != left || w.Top != top))
                {
                    w.Left = left;
                    w.Top = top;
                }
            }
            ApplyVisibility();
        }

        private Window Open(EventPanel panel)
        {
            var w = new BlazorWindow(App.AppHost.Services, typeof(EventPanelPage))
            {
                ShowInTaskbar = false,
                Title = "AION2 " + panel.Name,
                WindowStartupLocation = WindowStartupLocation.Manual,
                Left = panel.Left ?? 600,
                Top = panel.Top ?? 80,
            };
            w.BlazorWebView.RootComponents[0].Parameters = new Dictionary<string, object?> { [nameof(EventPanelPage.PanelId)] = panel.Id };
            windowManager.Open(Key, w, isSingleton: false, instanceId: panel.Id);
            windows[panel.Id] = w;
            ApplyClickThrough(panel.Id, w);
            return w;
        }

        private void ApplyClickThrough(string id, Window w)
        {
            if (Editing) windowManager.RestoreClickThrough(Key, id);
            else windowManager.SetClickThrough(Key, id);
        }

        /// <summary>"Вне боя": hidden while a fight is on; always shown while placing.</summary>
        private void ApplyVisibility()
        {
            var hide = !Editing && options.Current.EventPanelsOutOfCombatOnly && sessions.GetActiveTargetSession() is not null;
            foreach (var w in windows.Values)
            {
                if (hide && w.IsVisible) w.Hide();
                else if (!hide && !w.IsVisible) w.Show();
            }
        }
    }
}
