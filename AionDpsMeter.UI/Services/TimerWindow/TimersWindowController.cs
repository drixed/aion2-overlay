// aion2-overlay fork: new file (see FORK_CHANGES.md).
using AionDpsMeter.Core.Windowing;
using AionDpsMeter.UI.Pages;
using AionDpsMeter.UI.Services.Windowing;
using AionDpsMeter.UI.Utils;

namespace AionDpsMeter.UI.Services.TimerWindow
{
    /// <summary>The timers overlay: click-through over the game; Ctrl+Shift+T toggles edit mode (drag, buttons, filters).</summary>
    public sealed class TimersWindowController(IWindowManagerService windowManager)
    {
        /// <summary>Deliberately not a member of upstream's WindowKey enum (no edit to their file): the window manager
        /// keys windows by value and saves bounds under key.ToString(), i.e. "1001".</summary>
        public static readonly WindowKey Key = (WindowKey)1001;

        private const uint ModControl = 0x0002, ModShift = 0x0004, ModNoRepeat = 0x4000, VkT = 0x54;
        private const int HotkeyId = 9101;

        private GlobalHotkey? hotkey;

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
                WindowStartupLocation = System.Windows.WindowStartupLocation.Manual,
                Left = 20,
                Top = 420,
            };
            windowManager.Open(Key, window, isSingleton: true, persistenceMode: WindowPersistenceMode.OnlyPosition);
            windowManager.SetClickThrough(Key);

            hotkey = new GlobalHotkey(window);
            hotkey.Register(ModControl | ModShift | ModNoRepeat, VkT, HotkeyId);
            hotkey.HotkeyPressed += ToggleEditing;
        }

        public void ToggleEditing()
        {
            IsEditing = !IsEditing;
            if (IsEditing) windowManager.RestoreClickThrough(Key);
            else windowManager.SetClickThrough(Key);
            EditingChanged?.Invoke();
        }

        public void Drag() => windowManager.Drag(Key);
    }
}
