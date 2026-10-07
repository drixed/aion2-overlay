using AionDpsMeter.Services.PacketProcessing.Fork;
using AionDpsMeter.Timers.Bosses;
using AionDpsMeter.Timers.Feed;
using AionDpsMeter.Timers.Runtime;
using AionDpsMeter.Timers.Schedule;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AionDpsMeter.Timers;

public static class TimersServiceCollectionExtensions
{
    public static IServiceCollection AddTimers(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton(_ => BossCatalog.LoadDefault());
        services.AddSingleton(_ => new ScheduleSource(
            new HttpClient { Timeout = TimeSpan.FromSeconds(15) },
            new Uri(ScheduleSource.DefaultRemote),
            TimersPaths.Of("schedule-cache.json"),
            ScheduleSource.ReadEmbedded));
        services.AddSingleton(sp => new FieldBossTracker(
            sp.GetRequiredService<BossCatalog>(),
            () => sp.GetRequiredService<ScheduleSource>().Current,
            sp.GetRequiredService<TimeProvider>()));
        services.AddSingleton(_ => new TimerStateStore(TimersPaths.Of("timers-state.json")));
        services.AddSingleton(_ =>
        {
            var store = new TimersOptionsStore(TimersPaths.Of("timers-settings.json"));
            store.Load();
            store.Save(); // writes the defaults once so the file is there to edit
            return store;
        });
        services.AddSingleton<IServerContext, EntityTrackerServerContext>();
        services.AddSingleton<IMobHpSource, EntityTrackerHpSource>();
        services.AddSingleton<KillWatcher>();
        services.AddSingleton<AlertService>();
        services.AddSingleton<IFieldBossListListener, FieldBossListListener>();
        services.AddHostedService<TimersHostedService>();
        return services;
    }
}
