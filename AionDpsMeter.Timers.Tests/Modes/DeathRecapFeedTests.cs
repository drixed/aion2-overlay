using AionDpsMeter.Services.PacketProcessing.Fork;
using AionDpsMeter.Services.Services.Entity;
using AionDpsMeter.Timers.Modes;
using Microsoft.Extensions.DependencyInjection;

namespace AionDpsMeter.Timers.Tests.Modes;

/// <summary>
/// Seen in game: the recap listed allies' heals ("Light of Regeneration") and none of the boss's hits — heals come in
/// the same packet as hits, and upstream drops monster skills (codes 1 000 000–9 999 999) before they reach the feed.
/// </summary>
public class DeathRecapFeedTests
{
    [Fact]
    public void Heals_stay_out_of_the_recap_and_monster_hits_get_in()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<EntityTracker>();
        services.AddTimers();
        using var provider = services.BuildServiceProvider();
        var entities = provider.GetRequiredService<EntityTracker>();
        var modes = provider.GetRequiredService<MeterModesService>();
        const int me = 424_242, ally = 424_243, boss = 424_244;
        entities.GetOrCreateSessionPlayer(me);
        entities.GetOrCreateSessionPlayer(ally);
        var now = DateTime.Now;

        CombatFeed.PublishHit(new RawHit(ally, me, 1_790, 18120000, false, now.AddSeconds(-2))); // a heal skill
        CombatFeed.PublishHit(new RawHit(boss, me, 1_797, 1228220, false, now.AddSeconds(-1)));  // the boss's hit
        CombatFeed.PublishDeath(me, now);

        var recap = modes.Deaths.LastFor(me)!;
        Assert.Equal([1_797L], recap.Hits.Select(h => h.Damage));
    }
}
