using AionDpsMeter.Services.PacketProcessing.Fork;
using AionDpsMeter.Services.Services.Session.Persistence;
using AionDpsMeter.Timers.Bosses;
using AionDpsMeter.Timers.Records;
using Microsoft.Extensions.Logging;
using AionDpsMeter.Timers.Energy;
using AionDpsMeter.Timers.Zones;
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
        services.AddSingleton(_ =>
        {
            var catalog = BossCatalog.LoadDefault();
            AionDpsMeter.Core.Models.Mob.ForkBoss = catalog.IsBoss; // bosses upstream's mob table misses
            return catalog;
        });
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
            store.LoadOrCreate();
            return store;
        });
        services.AddSingleton<IServerContext, EntityTrackerServerContext>();
        services.AddSingleton<IMobHpSource, EntityTrackerHpSource>();
        services.AddSingleton<KillWatcher>();
        services.AddSingleton<AlertService>();
        services.AddSingleton<IFieldBossListListener, FieldBossListListener>();
        services.AddSingleton(sp => new EnergyTracker(sp.GetRequiredService<TimeProvider>(), TimersPaths.Of("energy-state.json")));
        services.AddSingleton<IEnergyListener, EnergyListener>();
        services.AddSingleton<ZoneTracker>();
        services.AddSingleton<IMapLoadListener, MapLoadListener>();
        services.AddSingleton(sp => new BossRecords(TimersPaths.Of("boss-records.json"),
            sp.GetRequiredService<TimeProvider>(), sp.GetRequiredService<ILogger<BossRecords>>()));
        // Upstream's history store (AddCombatHistoryPersistence, registered before us), wrapped so every saved fight
        // reaches the records. Without upstream's store (tests) there is nothing to wrap.
        if (services.Any(d => d.ServiceType == typeof(ICombatHistoryStore)))
        {
            services.TryAddSingleton<CombatHistoryStore>();
            services.AddSingleton<ICombatHistoryStore, RecordingHistoryStore>();
        }
        services.AddSingleton<AionDpsMeter.Timers.Modes.MeterModesService>();
        services.AddSingleton(sp => new AionDpsMeter.Timers.Pets.PetTracker(TimersPaths.Of("pets-state.json"),
            sp.GetRequiredService<BossCatalog>(), sp.GetRequiredService<TimeProvider>(),
            sp.GetRequiredService<ILogger<AionDpsMeter.Timers.Pets.PetTracker>>()));
        services.AddSingleton<IPetListener>(sp => sp.GetRequiredService<AionDpsMeter.Timers.Pets.PetTracker>());
        services.AddSingleton(_ => new AionDpsMeter.Timers.Character.CharacterTracker(TimersPaths.Of("character.json")));
        services.AddSingleton<AionDpsMeter.Services.PacketProcessing.Fork.IRawPacketListener>(sp => sp.GetRequiredService<AionDpsMeter.Timers.Character.CharacterTracker>());
        services.AddSingleton(_ => new AionDpsMeter.Timers.Replay.PositionLog(TimersPaths.Of("Replays")));
        services.AddSingleton(_ =>
        {
            var recorder = new AionDpsMeter.Timers.Diagnostics.PacketRecorder(Path.Combine(AppContext.BaseDirectory, "Logs"));
            AionDpsMeter.Services.PacketProcessing.Fork.PacketTap.Frame += (opcode, packet) => recorder.Record(DateTime.Now, opcode, packet.Data);
            return recorder;
        });
        services.AddSingleton<AionDpsMeter.Services.PacketProcessing.Fork.IRawPacketListener>(sp => sp.GetRequiredService<AionDpsMeter.Timers.Replay.PositionLog>());
        services.AddSingleton<AionDpsMeter.Timers.Pets.TargetSelection>();
        services.AddSingleton(_ => new AionDpsMeter.Timers.Craft.CraftStock(AionDpsMeter.Timers.Craft.CraftBook.Default.Knows));
        services.AddSingleton<AionDpsMeter.Services.PacketProcessing.Fork.IRawPacketListener>(sp => sp.GetRequiredService<AionDpsMeter.Timers.Craft.CraftStock>());
        services.AddSingleton(_ => new AionDpsMeter.Timers.Craft.CraftPlan(TimersPaths.Of("craft-plan.json")));
        services.AddSingleton(_ =>
        {
            // 0x610C belongs to the energy forwarder, so the checklist listens to every frame like the lobby
            var checklist = new AionDpsMeter.Timers.Checklist.ChecklistTracker(TimersPaths.Of("checklist.json"));
            AionDpsMeter.Services.PacketProcessing.Fork.PacketTap.Frame += (opcode, packet) => checklist.OnRawPacket(opcode, packet);
            return checklist;
        });
        services.AddSingleton<AionDpsMeter.Services.PacketProcessing.Fork.IRawPacketListener>(sp =>
        {
            var entities = sp.GetRequiredService<AionDpsMeter.Services.Services.Entity.EntityTracker>();
            var places = sp.GetRequiredService<AionDpsMeter.Timers.Replay.PositionLog>();
            return new AionDpsMeter.Timers.Lobby.PartyLinker(
                () => entities.PlayerEntities.Where(p => !p.IsUser).Select(p => (p.Id, p.IsIdentified && !string.IsNullOrEmpty(p.Name), p.GlobalId)),
                places.Last,
                entities.LinkSessionToGlobalPlayer);
        });
        services.AddSingleton(_ => new AionDpsMeter.Timers.Character.InspectedPlayers(TimersPaths.Of("inspected.json")));
        services.AddSingleton<AionDpsMeter.Services.PacketProcessing.Fork.IRawPacketListener>(sp => sp.GetRequiredService<AionDpsMeter.Timers.Character.InspectedPlayers>());
        services.AddSingleton(_ =>
        {
            // 0x9702 belongs to upstream's party processor, so the lobby listens to every frame instead of RawForwarder
            var lobby = new AionDpsMeter.Timers.Lobby.LobbyTracker();
            AionDpsMeter.Services.PacketProcessing.Fork.PacketTap.Frame += (opcode, packet) => lobby.OnRawPacket(opcode, packet);
            return lobby;
        });
        services.AddSingleton<AionDpsMeter.Services.PacketProcessing.Fork.IRawPacketListener>(sp => sp.GetRequiredService<AionDpsMeter.Timers.Pets.TargetSelection>());
        services.AddSingleton<AionDpsMeter.Timers.Farm.FarmIncome>();
        services.AddSingleton<AionDpsMeter.Services.PacketProcessing.Fork.IRawPacketListener>(sp => sp.GetRequiredService<AionDpsMeter.Timers.Farm.FarmIncome>());
        services.AddHostedService<TimersHostedService>();
        return services;
    }
}
