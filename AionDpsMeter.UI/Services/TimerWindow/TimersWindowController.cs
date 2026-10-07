// aion2-overlay fork: new file (see FORK_CHANGES.md).
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using AionDpsMeter.Core.Windowing;
using AionDpsMeter.Timers.Feed;
using AionDpsMeter.UI.Pages;
using AionDpsMeter.UI.Services.Windowing;
using AionDpsMeter.UI.Utils;
using Microsoft.Extensions.Logging;

namespace AionDpsMeter.UI.Services.TimerWindow
{
    /// <summary>The timers overlay: click-through over the game; the edit hotkey (timers-settings.json, default
    /// Ctrl+Shift+F9) toggles edit mode (drag, buttons, filters).</summary>
    public sealed class TimersWindowController(IWindowManagerService windowManager, TimersOptionsStore options, ILogger<TimersWindowController> logger)
    {
        /// <summary>Deliberately not a member of upstream's WindowKey enum (no edit to their file): the window manager
        /// keys windows by value and saves bounds under key.ToString(), i.e. "1001".</summary>
        public static readonly WindowKey Key = (WindowKey)1001;

        private const int WmHotkey = 0x0312;
        private const int HotkeyId = 9101;
        private const uint ModNoRepeat = 0x4000;

        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        public bool IsEditing { get; private set; }

        public event Action? EditingChanged;

        public void Open()
        {
            var window = new BlazorWindow(App.AppHost.Services, typeof(TimersOverlay))
            {
                Width = 340,
                Height = 460,
                ShowInTaskbar = false,
                Title = "AION2 Timers",
                // First run only: below the DPS window instead of on top of it. A saved position wins.
                WindowStartupLocation = WindowStartupLocation.Manual,
                Left = 20,
                Top = 420,
            };
            windowManager.Open(Key, window, isSingleton: true, persistenceMode: WindowPersistenceMode.OnlyPosition);
            windowManager.SetClickThrough(Key);
            RegisterEditHotkey(window);
        }

        public void ToggleEditing()
        {
            IsEditing = !IsEditing;
            if (IsEditing) windowManager.RestoreClickThrough(Key);
            else windowManager.SetClickThrough(Key);
            EditingChanged?.Invoke();
        }

        public void Drag() => windowManager.Drag(Key);

        /// <summary>Upstream's GlobalHotkey hides a failed registration; this one says so in the log, because without
        /// the hotkey there is no way into edit mode.</summary>
        private void RegisterEditHotkey(Window window)
        {
            var text = options.Current.EditHotkey;
            var (modifiers, vk) = HotkeyParser.Parse(text);
            if (vk == 0)
            {
                if (!string.IsNullOrWhiteSpace(text)) logger.LogWarning("Timers edit hotkey '{Hotkey}' is not a valid key", text);
                return;
            }

            var handle = new WindowInteropHelper(window).Handle;
            if (!RegisterHotKey(handle, HotkeyId, modifiers | ModNoRepeat, vk))
            {
                logger.LogWarning("Timers edit hotkey '{Hotkey}' is taken by another program; change EditHotkey in timers-settings.json", text);
                return;
            }
            HwndSource.FromHwnd(handle)?.AddHook((IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled) =>
            {
                if (msg == WmHotkey && wParam.ToInt32() == HotkeyId)
                {
                    ToggleEditing();
                    handled = true;
                }
                return IntPtr.Zero;
            });
        }
    }
}
