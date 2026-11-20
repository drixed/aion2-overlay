using AionDpsMeter.Timers.Map;

namespace AionDpsMeter.Timers.Tests.Map;

public class WorldMapTests
{
    [Fact]
    public void Verteron_and_Altgard_come_with_their_points()
    {
        var verteron = WorldMap.For(1010);
        Assert.NotNull(verteron);
        Assert.Equal("Бертрон", verteron!.Name);
        Assert.Equal(24, verteron.Bosses.Count);
        Assert.True(verteron.PetSpawns.Count > 100);
        Assert.True(verteron.Cubes.Count > 1000);
        Assert.Contains(verteron.Gather, g => WorldMap.Object(g.Key)?.Kind == "Herb");

        Assert.Equal("Альтгард", WorldMap.For(1110)?.Name);
        Assert.Same(verteron, WorldMap.For(902));    // another zone id of the same map
        Assert.Null(WorldMap.For(4242));
    }

    [Fact]
    public void Monsters_lead_to_their_pets_with_russian_names()
    {
        Assert.Equal(1026, WorldMap.PetOf(2000000));
        Assert.Equal("Воин краллов", WorldMap.PetName(1026));
        Assert.Null(WorldMap.PetOf(1));
    }

    [Fact]
    public void Gathering_kinds_have_russian_labels()
    {
        Assert.Equal("Травы", WorldMap.KindLabel("Herb"));
        Assert.Equal("Руда", WorldMap.KindLabel("Ore"));
        Assert.Equal("Прочее", WorldMap.KindLabel("Something"));
    }

    [Fact]
    public void The_nearest_point_and_its_distance_in_metres()
    {
        MapPoint[] points = [new(0, 0), new(1000, 0), new(5000, 5000)];
        var (point, metres) = WorldMap.Nearest(points, new MapPoint(900, 0))!.Value;
        Assert.Equal(new MapPoint(1000, 0), point);
        Assert.Equal(1, metres);                     // game units are centimetres
        Assert.Null(WorldMap.Nearest([], new MapPoint(0, 0)));
    }

    [Fact]
    public void Every_pet_has_a_name_even_when_the_schedule_does_not_know_it()
    {
        var data = AionDpsMeter.Timers.Schedule.ScheduleData.Empty;
        Assert.Equal(WorldMap.PetName(1001), data.PetName(1001));
        Assert.NotNull(data.PetName(1001));
        Assert.Null(data.PetName(99999));
    }
}
