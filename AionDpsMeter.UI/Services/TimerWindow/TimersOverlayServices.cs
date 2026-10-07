// aion2-overlay fork: new file (see FORK_CHANGES.md).
using AionDpsMeter.Timers;
using Microsoft.Extensions.DependencyInjection;

namespace AionDpsMeter.UI.Services.TimerWindow
{
    public static class TimersOverlayServices
    {
        public static IServiceCollection AddTimersOverlay(this IServiceCollection services)
        {
            services.AddTimers();
            services.AddSingleton<TimersWindowController>();
            return services;
        }
    }
}
