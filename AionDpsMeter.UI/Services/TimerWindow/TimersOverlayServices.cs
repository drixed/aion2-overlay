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
            services.AddTimers();
            services.AddSingleton<TimersWindowController>();
            services.AddSingleton<OverlayRuntime>();
            services.AddSingleton<CubeMapWindow>();
            services.AddSingleton<AlertRunner>();
            services.AddSingleton<WindowBoundsKeeper>();
            return services;
        }
    }
}
