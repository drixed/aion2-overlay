using AionDpsMeter.Services.PacketProcessing.Fork;
using AionDpsMeter.Services.Services.Entity;
using AionDpsMeter.Timers.Bosses;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AionDpsMeter.Timers.Tests.Runtime;

public class StartupSmokeTests
{
    /// <summary>Resolves the module the way the app does, with upstream's real mobs.json next to the binaries,
    /// so a data or DI change upstream fails CI instead of the user's app start.</summary>
    [Fact]
    public void The_timers_module_resolves_with_upstreams_real_data()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<EntityTracker>();
        services.AddTimers();
        using var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetRequiredService<IFieldBossListListener>());
        Assert.Contains(provider.GetServices<IHostedService>(), s => s.GetType().Name == "TimersHostedService");
        Assert.Equal(24, provider.GetRequiredService<BossCatalog>().FieldBossesInBlock(2400).Count);
    }
}
