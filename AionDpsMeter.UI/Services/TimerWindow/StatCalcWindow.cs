// aion2-overlay fork: new file (see FORK_CHANGES.md).
using System.Windows;
using AionDpsMeter.Core.Windowing;
using AionDpsMeter.UI.Pages;
using AionDpsMeter.UI.Services.Windowing;

namespace AionDpsMeter.UI.Services.TimerWindow
{
    /// <summary>The stat calculator in the fork's design (Russian, dark with a teal accent); upstream's ⚔ opens it instead of
    /// its WPF window. The maths is upstream's <see cref="ViewModels.StatEfficiencyCalculatorViewModel"/>.</summary>
    public sealed class StatCalcWindow(IWindowManagerService windowManager, WindowBoundsKeeper bounds)
    {
        /// <summary>Not in upstream's WindowKey enum, after the history views (1007–1009).</summary>
        public static readonly WindowKey Key = (WindowKey)1010;

        public event Action? Requested;

        public void Open()
        {
            if (!windowManager.IsOpen(Key))
            {
                var w = new BlazorWindow(App.AppHost.Services, typeof(StatCalcPage))
                {
                    Width = 1080,
                    Height = 760,
                    ShowInTaskbar = false,
                    Title = "AION2 Калькулятор статов",
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
