// aion2-overlay fork: new file (see FORK_CHANGES.md).
using System.Windows;
using System.Windows.Threading;
using AionDpsMeter.Services.Services.Session;
using AionDpsMeter.Timers.Feed;
using AionDpsMeter.Timers.Overlay;
using Microsoft.Extensions.Logging;
using Forms = System.Windows.Forms;

namespace AionDpsMeter.UI.Services.TimerWindow
{
    /// <summary>
    /// The tray icon and the three tray settings of the reference meter: "Сворачивать в трей" (the main window's "_"),
    /// "В трей при закрытии игры" and "Скрывать в простое". The icon shows while one of them is on or the overlay is in the
    /// tray; a double click or "Показать метр" brings back exactly the windows that were hidden.
    /// </summary>
    public sealed class TrayService(TimersOptionsStore options, CombatSessionManager sessions, HistoryBrowserWindow history,
        StatCalcWindow calc, NewsWindow news, CharacterWindow character, MapWindow map, ILogger<TrayService> logger)
    {
        private readonly List<Window> hidden = [];
        private Forms.NotifyIcon? icon;
        private Window? main;
        private DispatcherTimer? timer;
        private bool hiddenByRule, toldOnce, userShownWhileClosed;
        private bool? lastGame;
        private DateTimeOffset lastFight = DateTimeOffset.Now;

        public void Start(Window mainWindow)
        {
            if (main is not null) return;
            main = mainWindow;
            main.StateChanged += OnStateChanged;
            options.Changed += () => main.Dispatcher.BeginInvoke(UpdateIcon);
            Application.Current.Exit += (_, _) => icon?.Dispose();
            UpdateIcon();
            timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            timer.Tick += (_, _) => Tick();
            timer.Start();
        }

        private bool Wanted => options.Current is { } o && (o.MinimizeToTray || o.TrayWhenGameClosed || o.HideWhenIdleMinutes > 0);

        private void UpdateIcon()
        {
            try
            {
                if (!Wanted && hidden.Count == 0)
                {
                    if (icon is not null) icon.Visible = false;
                    return;
                }
                icon ??= CreateIcon();
                icon.Visible = true;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Tray icon failed");
            }
        }

        private Forms.NotifyIcon CreateIcon()
        {
            var tray = new Forms.NotifyIcon
            {
                Text = "AION2 Overlay",
                Icon = System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath ?? "") ?? System.Drawing.SystemIcons.Application,
            };
            var menu = new Forms.ContextMenuStrip();
            menu.Items.Add("Показать метр", null, (_, _) => OnUi(() => ShowAll(byUser: true)));
            menu.Items.Add("История боёв", null, (_, _) => OnUi(() => { ShowAll(byUser: true); history.Open(); }));
            menu.Items.Add("Персонаж", null, (_, _) => OnUi(() => { ShowAll(byUser: true); character.Open(); }));
            menu.Items.Add("Карта", null, (_, _) => OnUi(() => { ShowAll(byUser: true); map.Open(); }));
            menu.Items.Add("Калькулятор статов", null, (_, _) => OnUi(() => { ShowAll(byUser: true); calc.Open(); }));
            menu.Items.Add("Объявления и Twitch Drops", null, (_, _) => OnUi(() => { ShowAll(byUser: true); news.Open(); }));
            menu.Items.Add(new Forms.ToolStripSeparator());
            menu.Items.Add("Выход", null, (_, _) => OnUi(() => { ShowAll(byUser: true); main?.Close(); }));
            tray.ContextMenuStrip = menu;
            tray.DoubleClick += (_, _) => OnUi(() => ShowAll(byUser: true));
            return tray;
        }

        private void OnUi(Action action) => main?.Dispatcher.BeginInvoke(action);

        /// <summary>"Сворачивать в трей": the main window's minimize hides the overlay instead.</summary>
        private void OnStateChanged(object? sender, EventArgs e)
        {
            if (main is null || !options.Current.MinimizeToTray || main.WindowState != WindowState.Minimized) return;
            main.WindowState = WindowState.Normal;
            HideAll(byRule: false);
        }

        private void Tick()
        {
            try
            {
                var running = ForkRuntime.GameRunning();
                if (lastGame != running) { userShownWhileClosed = false; lastGame = running; }
                var now = DateTimeOffset.Now;
                if (sessions.GetActiveTargetSession() is not null) lastFight = now;
                var focused = Application.Current.Windows.OfType<Window>().Any(w => w.IsActive);
                var reason = TrayRules.Reason(options.Current, running, now, lastFight, focused);
                if (reason == TrayReason.GameClosed && userShownWhileClosed) reason = TrayReason.None;

                if (reason != TrayReason.None && hidden.Count == 0) HideAll(byRule: true);
                else if (reason == TrayReason.None && hiddenByRule && hidden.Count > 0) ShowAll(byUser: false);
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Tray check failed");
            }
        }

        private void HideAll(bool byRule)
        {
            foreach (Window w in Application.Current.Windows)
            {
                if (!w.IsVisible) continue;
                w.Hide();
                hidden.Add(w);
            }
            if (hidden.Count == 0) return;
            hiddenByRule = byRule;
            UpdateIcon();
            if (!toldOnce && icon is not null)
            {
                toldOnce = true;
                icon.ShowBalloonTip(3000, "AION2 Overlay", "Метр в трее — двойной клик по значку вернёт его.", Forms.ToolTipIcon.Info);
            }
        }

        private void ShowAll(bool byUser)
        {
            foreach (var w in hidden)
            {
                w.Show();
                if (w.WindowState == WindowState.Minimized) w.WindowState = WindowState.Normal;
            }
            hidden.Clear();
            hiddenByRule = false;
            if (byUser)
            {
                lastFight = DateTimeOffset.Now; // the idle countdown starts over
                if (lastGame == false) userShownWhileClosed = true;
                main?.Activate();
            }
            UpdateIcon();
        }
    }
}
