// aion2-overlay fork: new file (see FORK_CHANGES.md).
using AionDpsMeter.Timers;
using Microsoft.Extensions.DependencyInjection;

namespace AionDpsMeter.UI.Services.TimerWindow
{
    public static class TimersOverlayServices
    {
        public static IServiceCollection AddTimersOverlay(this IServiceCollection services)
        {
            ForkCulture.Apply(); // runs during host setup, before any window renders a number
            // upstream reads appsettings.user.json from the current folder; a shortcut, the updater or Windows' autostart
            // may start the meter elsewhere, so it always works in its own folder
            try { System.IO.Directory.SetCurrentDirectory(AppContext.BaseDirectory); } catch (System.IO.IOException) { }
            services.AddTimers();
            services.AddSingleton<TimersWindowController>();
            services.AddSingleton<OverlayRuntime>();
            services.AddSingleton<CubeMapWindow>();
            services.AddSingleton<EventPanelsController>();
            services.AddSingleton<FightSummaryWindow>();
            services.AddSingleton<HistoryBrowserWindow>();
            services.AddSingleton<HistoryViewWindow>();
            services.AddSingleton<StatCalcWindow>();
            services.AddSingleton<TrayService>();
            services.AddSingleton<NewsService>();
            services.AddSingleton<NewsWindow>();
            services.AddSingleton<BuffTrackerService>();
            services.AddSingleton<CharacterWindow>();
            services.AddSingleton<MapWindow>();
            services.AddSingleton<AlertRunner>();
            services.AddSingleton<WindowBoundsKeeper>();
            services.AddSingleton<AlertSoundPlayer>();
            return services;
        }
    }
}
