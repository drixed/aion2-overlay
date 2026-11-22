// aion2-overlay fork: new file (see FORK_CHANGES.md).
namespace AionDpsMeter.Timers.Map;

/// <summary>A hidden cube close by: where it is, how far in metres and which way ("северо-восток").</summary>
public sealed record CubeNear(MapPoint Cube, int Metres, string Direction);

/// <summary>
/// "Куб рядом": tells once about each hidden cube that comes within the set distance of the player (own position only,
/// from the table of cube places), with the way to it as a compass point (north is up on the map, towards smaller Y).
/// A cube is told about again only after the player went twice that distance away from it.
/// </summary>
public sealed class CubeRadar
{
    private static readonly string[] Points = ["север", "северо-восток", "восток", "юго-восток", "юг", "юго-запад", "запад", "северо-запад"];
    private readonly HashSet<MapPoint> told = [];

    /// <summary>The way from one place to another in degrees, 0 = north (up on the map), clockwise: the compass arrow.</summary>
    public static double Bearing(MapPoint from, MapPoint to) =>
        (Math.Atan2(to.X - from.X, -(to.Y - from.Y)) * 180 / Math.PI + 360) % 360;

    public static string Direction(MapPoint from, MapPoint to) => Points[(int)Math.Round(Bearing(from, to) / 45) % 8];

    public static int Metres(MapPoint a, MapPoint b) => (int)Math.Round(Math.Sqrt(Sq(a, b)) / 100);

    /// <summary>The nearest cube within <paramref name="radiusMetres"/> not told about yet; null when there is none.</summary>
    public CubeNear? Check(IReadOnlyCollection<MapPoint> cubes, MapPoint me, int radiusMetres)
    {
        var far = Math.Pow(radiusMetres * 2 * 100.0, 2);
        told.RemoveWhere(c => Sq(c, me) > far);
        var radius = Math.Pow(radiusMetres * 100.0, 2);
        MapPoint? best = null;
        var bestSq = double.MaxValue;
        foreach (var c in cubes)
        {
            var sq = Sq(c, me);
            if (sq <= radius && sq < bestSq && !told.Contains(c)) (best, bestSq) = (c, sq);
        }
        if (best is not { } cube) return null;
        told.Add(cube);
        return new CubeNear(cube, Metres(me, cube), Direction(me, cube));
    }

    /// <summary>A screenshot of the game map placed over the land at first: centred on it, covering it, its shape kept.
    /// Returns its top-left corner and width in game units (the height follows the picture).</summary>
    public static (double X, double Y, double Width) FitUnderlay(double minX, double minY, double maxX, double maxY, int imageWidth, int imageHeight)
    {
        var aspect = imageWidth / (double)Math.Max(1, imageHeight);
        var width = Math.Max(maxX - minX, (maxY - minY) * aspect);
        var height = width / aspect;
        return ((minX + maxX) / 2 - width / 2, (minY + maxY) / 2 - height / 2, width);
    }

    private static double Sq(MapPoint a, MapPoint b)
    {
        var dx = (double)a.X - b.X;
        var dy = (double)a.Y - b.Y;
        return dx * dx + dy * dy;
    }
}
