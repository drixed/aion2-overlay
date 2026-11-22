using AionDpsMeter.Timers.Map;

namespace AionDpsMeter.Timers.Tests.Map;

public class CubeRadarTests
{
    private static readonly MapPoint Me = new(0, 0);

    // game units are centimetres; on the map north is up, i.e. towards smaller Y
    [Theory]
    [InlineData(0, -4000, "север")]
    [InlineData(3000, -3000, "северо-восток")]
    [InlineData(4000, 0, "восток")]
    [InlineData(3000, 3000, "юго-восток")]
    [InlineData(0, 4000, "юг")]
    [InlineData(-3000, 3000, "юго-запад")]
    [InlineData(-4000, 0, "запад")]
    [InlineData(-3000, -3000, "северо-запад")]
    public void The_direction_reads_as_a_compass_point(float x, float y, string expected) =>
        Assert.Equal(expected, CubeRadar.Direction(Me, new MapPoint(x, y)));

    [Theory]
    [InlineData(0, -4000, 0)]
    [InlineData(4000, 0, 90)]
    [InlineData(0, 4000, 180)]
    [InlineData(-4000, 0, 270)]
    public void The_compass_arrow_turns_clockwise_from_north(float x, float y, double degrees) =>
        Assert.Equal(degrees, CubeRadar.Bearing(Me, new MapPoint(x, y)), 3);

    [Fact]
    public void A_cube_in_range_is_announced_once_with_how_far_and_where()
    {
        var radar = new CubeRadar();
        var cubes = new[] { new MapPoint(3000, -3000), new MapPoint(90_000, 0) };
        var near = radar.Check(cubes, Me, radiusMetres: 50);
        Assert.Equal(new CubeNear(cubes[0], 42, "северо-восток"), near);
        Assert.Null(radar.Check(cubes, new MapPoint(500, -500), 50)); // the same cube: not again
    }

    [Fact]
    public void Nothing_is_announced_when_no_cube_is_in_range() =>
        Assert.Null(new CubeRadar().Check([new MapPoint(9000, 0)], Me, radiusMetres: 50));

    [Fact]
    public void A_cube_is_announced_again_after_going_well_away_and_coming_back()
    {
        var radar = new CubeRadar();
        MapPoint[] cubes = [new MapPoint(2000, 0)];
        Assert.NotNull(radar.Check(cubes, Me, 50));
        Assert.Null(radar.Check(cubes, new MapPoint(-6000, 0), 50));   // 80 m away: not yet far enough
        Assert.Null(radar.Check(cubes, new MapPoint(-12000, 0), 50));  // 140 m away: forgotten
        Assert.NotNull(radar.Check(cubes, Me, 50));
    }

    [Fact]
    public void A_screenshot_is_fitted_over_the_land_keeping_its_shape()
    {
        // land 100 000 × 50 000, screenshot 1000 × 1000 px: as wide as the land is long, centred on it
        var (x, y, width) = CubeRadar.FitUnderlay(minX: 0, minY: 0, maxX: 100_000, maxY: 50_000, imageWidth: 1000, imageHeight: 1000);
        Assert.Equal((0d, -25_000d, 100_000d), (x, y, width));
    }
}
