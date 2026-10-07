using AionDpsMeter.Services.Services.Entity;
using AionDpsMeter.Timers.Runtime;
using Microsoft.Extensions.Logging.Abstractions;

namespace AionDpsMeter.Timers.Tests.Runtime;

public class EntityTrackerAdaptersTests
{
    private static readonly NullLogger<EntityTrackerServerContext> Log = NullLogger<EntityTrackerServerContext>.Instance;

    [Fact]
    public void A_server_missing_from_upstreams_table_stays_zero_even_in_a_party()
    {
        // EU server ids are not in RATmeter's ServerMap: the name is empty. Party data would give a raw id, but using it
        // would split the timers every time the player joins a party.
        var entities = new EntityTracker();
        entities.SetSessionPlayerName(10, "Me", 0, "", isUser: true);
        entities.GetPlayerEntity(10)!.ServerId = 3001;

        Assert.Equal(0, new EntityTrackerServerContext(entities, Log).CurrentServerId);
    }

    [Fact]
    public void A_solo_players_server_comes_from_the_server_name()
    {
        // Upstream only sets ServerName for the user's own character; ServerId stays 0 outside a party.
        var entities = new EntityTracker();
        entities.SetSessionPlayerName(10, "Me", 0, "ISR", isUser: true);

        Assert.Equal(2001, new EntityTrackerServerContext(entities, Log).CurrentServerId);
    }

    [Fact]
    public void After_a_character_switch_the_newest_character_counts()
    {
        var entities = new EntityTracker();
        entities.SetSessionPlayerName(10, "Old", 0, "ISR", isUser: true);
        Thread.Sleep(30);
        entities.SetSessionPlayerName(20, "New", 0, "TRI", isUser: true);

        Assert.Equal(2003, new EntityTrackerServerContext(entities, Log).CurrentServerId);
    }

    [Fact]
    public void No_character_yet_means_server_zero()
    {
        Assert.Equal(0, new EntityTrackerServerContext(new EntityTracker(), Log).CurrentServerId);
    }
}
