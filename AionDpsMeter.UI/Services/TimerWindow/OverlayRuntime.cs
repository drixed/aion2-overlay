// aion2-overlay fork: new file (see FORK_CHANGES.md).
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using AionDpsMeter.Core.Windowing;
using AionDpsMeter.Services.Services.Entity;
using AionDpsMeter.Services.Services.Session;
using AionDpsMeter.Timers.Feed;
using AionDpsMeter.Timers.Overlay;
using AionDpsMeter.UI.Services.Windowing;
using AionDpsMeter.UI.Utils;
using Microsoft.Extensions.Logging;

namespace AionDpsMeter.UI.Services.TimerWindow
{
    /// <summary>Overlay behaviour that lives outside the pages: "only over the game" (topmost follows the foreground
    /// window) and the six global hotkeys of the Abyss meter. Runs on the UI thread.</summary>
    public sealed class OverlayRuntime(
        TimersOptionsStore options,
        CombatSessionManager sessions,
        EntityTracker entities,
        IWindowManagerService windowManager,
        ILogger<OverlayRuntime> logger)
    {
        private const int WmHotkey = 0x0312;
        private const uint ModNoRepeat = 0x4000;

        private enum HotkeyId { HideOverlay = 9201, ResetFight, ClearMeter, CopySummary, Compact, ClickThrough }

        [DllImport("user32.dll")] private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
        [DllImport("user32.dll")] private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        private readonly HashSet<Window> loweredWindows = new();
        private readonly List<Window> hiddenWindows = new();
        private HwndSource? source;
        private DispatcherTimer? timer;
        private uint lastPid;
        private string? lastName;
        private bool clickThrough;

        /// <param name="host">A window that lives as long as the app (the main window): hotkeys are bound to its handle.</param>
        public void Start(Window host)
        {
            if (source is not null) return;
            var handle = new WindowInteropHelper(host).EnsureHandle();
            source = HwndSource.FromHwnd(handle);
            source?.AddHook(WndProc);
            RegisterHotkeys();
            options.Changed += () => host.Dispatcher.BeginInvoke(RegisterHotkeys);

            timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            timer.Tick += (_, _) => FollowForeground();
            timer.Start();
        }

        private void RegisterHotkeys()
        {
            if (source is null) return;
            var o = options.Current;
            Register(HotkeyId.HideOverlay, o.HideOverlayHotkey);
            Register(HotkeyId.ResetFight, o.ResetFightHotkey);
            Register(HotkeyId.ClearMeter, o.ClearMeterHotkey);
            Register(HotkeyId.CopySummary, o.CopySummaryHotkey);
            Register(HotkeyId.Compact, o.CompactHotkey);
            Register(HotkeyId.ClickThrough, o.ClickThroughHotkey);
        }

        private void Register(HotkeyId id, string text)
        {
            UnregisterHotKey(source!.Handle, (int)id);
            var (modifiers, vk) = HotkeyParser.Parse(text);
            if (vk == 0) return;
            if (!RegisterHotKey(source.Handle, (int)id, modifiers | ModNoRepeat, vk))
                logger.LogWarning("Hotkey {Hotkey} is taken by another program", text);
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg != WmHotkey) return IntPtr.Zero;
            try
            {
                switch ((HotkeyId)wParam.ToInt32())
                {
                    case HotkeyId.HideOverlay: ToggleOverlay(); break;
                    case HotkeyId.ResetFight: sessions.CompleteAndPersistActiveSessions(); break;
                    case HotkeyId.ClearMeter: sessions.Reset(); break;
                    case HotkeyId.CopySummary: CopySummary(); break;
                    case HotkeyId.Compact:
                        options.Current.Compact = !options.Current.Compact;
                        options.Save();
                        break;
                    case HotkeyId.ClickThrough: ToggleClickThrough(); break;
                    default: return IntPtr.Zero;
                }
                handled = true;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Hotkey action failed");
            }
            return IntPtr.Zero;
        }

        /// <summary>Hides every visible window of the meter; the same key brings back exactly those.</summary>
        private void ToggleOverlay()
        {
            if (hiddenWindows.Count > 0)
            {
                foreach (var w in hiddenWindows) w.Show();
                hiddenWindows.Clear();
                return;
            }
            foreach (Window w in Application.Current.Windows)
            {
                if (!w.IsVisible) continue;
                w.Hide();
                hiddenWindows.Add(w);
            }
        }

        /// <summary>Clicks pass through the main and timers windows to the game; the same key makes them clickable again.</summary>
        private void ToggleClickThrough()
        {
            clickThrough = !clickThrough;
            foreach (var key in new[] { WindowKey.Main, TimersWindowController.Key })
            {
                if (clickThrough) windowManager.SetClickThrough(key);
                else windowManager.RestoreClickThrough(key);
            }
        }

        private void CopySummary()
        {
            var stats = sessions.PlayerStats.Where(p => p.TotalDamage > 0).OrderByDescending(p => p.TotalDamage).ToList();
            IReadOnlyDictionary<long, double>? partyShares = null;
            if (options.Current.OnlyMyParty)
            {
                partyShares = PartyFilter.Shares(stats.Select(p =>
                    new PartyRow(p.PlayerId, p.IsUser, entities.GetPlayerEntity((int)p.PlayerId)?.CharacterLevel ?? 0, p.TotalDamage)));
                stats = stats.Where(p => partyShares.ContainsKey(p.PlayerId)).ToList();
            }
            if (stats.Count == 0) return;
            var active = options.Current.DpsMode == DpsMode.Active;
            var lines = stats.Select(p => new SummaryLine(p.PlayerName,
                active ? DpsMath.Active(p.TotalDamage, p.FirstHit, p.LastHit) : p.DamagePerSecond,
                partyShares?[p.PlayerId] ?? p.DamagePercentage));
            var target = sessions.GetActiveTargetInfo() is { MobCode: > 0 } mob ? mob.Name : "Бой";
            Clipboard.SetText(FightSummary.Format(target, sessions.GetCombatDuration(), lines, options.Current.SummaryMultiline));
        }

        /// <summary>With "only over the game" on, overlay windows are topmost only while AION 2 or the meter is in front.</summary>
        private void FollowForeground()
        {
            try
            {
                var onTop = !options.Current.OnlyOverGame || OverlayFocus.StaysOnTop(ForegroundProcessName());
                foreach (Window window in Application.Current.Windows)
                {
                    if (!onTop && window.Topmost)
                    {
                        window.Topmost = false;
                        loweredWindows.Add(window);
                    }
                    else if (onTop && loweredWindows.Remove(window))
                    {
                        window.Topmost = true;
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Foreground check failed");
            }
        }

        private string? ForegroundProcessName()
        {
            GetWindowThreadProcessId(GetForegroundWindow(), out var pid);
            if (pid == 0) return null;
            if (pid == lastPid) return lastName;
            lastPid = pid;
            try { lastName = Process.GetProcessById((int)pid).ProcessName; }
            catch (ArgumentException) { lastName = null; }
            return lastName;
        }
    }
}
