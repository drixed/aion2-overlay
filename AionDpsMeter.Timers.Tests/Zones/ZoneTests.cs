using AionDpsMeter.Services.PacketProcessing.Routing;
using AionDpsMeter.Timers.Schedule;
using AionDpsMeter.Timers.Zones;

namespace AionDpsMeter.Timers.Tests.Zones;

public class ZoneTests
{
    // Live EU map load into Altgard (2026-10-07 20:42:52), body after the opcode 21 36.
    private const string AltgardLoad = "0100000056040000520ECC00000000000AA17C47294DB6C7004839465DE2EC420000000000";

    [Fact]
    public void A_map_load_names_the_map()
    {
        Assert.Equal(1110, MapLoadParser.Parse(Convert.FromHexString(AltgardLoad)));
        Assert.Null(MapLoadParser.Parse([1, 0, 0, 0]));
        Assert.Null(MapLoadParser.Parse(Convert.FromHexString("0100000000000000")));
    }

    [Fact]
    public void The_zone_tracker_follows_map_loads_from_packets()
    {
        var zones = new ZoneTracker();
        var changes = 0;
        zones.Changed += () => changes++;
        var body = Convert.FromHexString(AltgardLoad);
        var listener = new MapLoadListener(zones);

        listener.OnMapLoad(new Packet { Data = [(byte)(body.Length + 4), 0x21, 0x36, .. body], ReceivedAt = 0 });
        listener.OnMapLoad(new Packet { Data = [(byte)(body.Length + 4), 0x21, 0x36, .. body], ReceivedAt = 0 }); // teleport in the same map

        Assert.Equal(1110, zones.CurrentMapId);
        Assert.Equal(1, changes);
    }

    [Fact]
    public void Cube_maps_come_from_the_schedule_with_a_fallback()
    {
        var data = ScheduleJson.Parse("""
            { "cubeMaps": { "default": "https://example.test/all", "1110": "https://example.test/altgard" } }
            """);
        Assert.Equal("https://example.test/altgard", data.CubeMapFor(1110));
        Assert.Equal("https://example.test/all", data.CubeMapFor(1010));
        Assert.Equal("https://example.test/all", data.CubeMapFor(null));
        Assert.StartsWith("https://", ScheduleJson.Parse(ScheduleSource.ReadEmbedded()).CubeMapFor(1110));
        Assert.Null(ScheduleData.Empty.CubeMapFor(1110));
    }
}
