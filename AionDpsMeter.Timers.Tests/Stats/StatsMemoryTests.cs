using System.Text.Json;
using AionDpsMeter.Core.Data;
using AionDpsMeter.Services.Models;
using AionDpsMeter.Services.PacketProcessing.Fork;

namespace AionDpsMeter.Timers.Tests.Stats;

public class StatsMemoryTests
{
    [Fact]
    public void The_last_stats_are_kept_and_written_next_to_the_meter()
    {
        var at = new DateTime(2026, 11, 20, 2, 30, 0);
        StatsMemory.Remember(new PlayerCharacterStats(new Dictionary<ushort, int> { [StatIds.AttackGear] = 3110 }), at);

        Assert.Equal(3110, StatsMemory.Last?.Stats.AttackGear);
        Assert.Equal(at, StatsMemory.Last?.CapturedAt);
        var file = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "player-stats.json")));
        Assert.Equal(3110, file.RootElement.GetProperty("Values").GetProperty("317").GetInt32());
    }
}
