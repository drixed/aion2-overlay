// aion2-overlay fork: new file (see FORK_CHANGES.md).
using System.Text.Json;

namespace AionDpsMeter.Timers.Map;

/// <summary>A place in game units (centimetres), as the movement packets and the data tables give it.</summary>
public readonly record struct MapPoint(float X, float Y);

public sealed record MapBoss(int Npc, MapPoint At, string Name);

public sealed record GatherObject(int Id, string Kind, string Name);

/// <summary>One open world map with what can be found on it.</summary>
public sealed record MapLayer(
    int Id,
    string Name,
    IReadOnlyList<MapBoss> Bosses,
    IReadOnlyDictionary<int, IReadOnlyList<MapPoint>> PetSpawns,   // monster code → where it spawns
    IReadOnlyDictionary<int, IReadOnlyList<MapPoint>> Gather,      // gathering object → its places
    IReadOnlyList<MapPoint> Cubes);

/// <summary>
/// "Карта": spawn points of the monsters that give pet souls, gathering points, hidden cubes and field boss places on
/// Verteron and Altgard (Data/maps.ru.json — factual coordinates and names from SkeeveAN/Aion-DPS-Meter, MIT; see
/// NOTICE.md). The map is drawn as a diagram from these points; no game image is used.
/// </summary>
public static class WorldMap
{
    private sealed record Data(
        Dictionary<int, MapLayer> Maps,
        Dictionary<int, GatherObject> Objects,
        Dictionary<int, int> PetOf,
        Dictionary<int, (string Name, string Category)> Pets);

    private static readonly Lazy<Data> Loaded = new(Load);

    public static IReadOnlyList<MapLayer> All => Loaded.Value.Maps.Values.Distinct().OrderBy(m => m.Id).ToList();

    /// <summary>The map for a map id from the map load packet (or another zone id of the same map); null when not known.</summary>
    public static MapLayer? For(int? mapId) => mapId is { } id ? Loaded.Value.Maps.GetValueOrDefault(id) : null;

    public static GatherObject? Object(int id) => Loaded.Value.Objects.GetValueOrDefault(id);

    /// <summary>The pet a monster gives souls to.</summary>
    public static int? PetOf(int monster) => Loaded.Value.PetOf.TryGetValue(monster, out var pet) ? pet : null;

    public static string? PetName(int pet) => Loaded.Value.Pets.TryGetValue(pet, out var p) ? p.Name : null;

    public static string? PetCategory(int pet) => Loaded.Value.Pets.TryGetValue(pet, out var p) ? p.Category : null;

    public static string KindLabel(string kind) => kind switch
    {
        "Herb" => "Травы",
        "Ore" => "Руда",
        "Gemstone" => "Самоцветы",
        "Food" => "Продукты",
        "Wood" => "Древесина",
        "Cotton" => "Хлопок",
        "Od" => "Од",
        "Fragment" => "Следы Владык",
        "Cube" => "Кубы",
        _ => "Прочее",
    };

    /// <summary>The closest point to <paramref name="me"/> and how far it is in metres.</summary>
    public static (MapPoint Point, int Metres)? Nearest(IEnumerable<MapPoint> points, MapPoint me)
    {
        MapPoint? best = null;
        var bestSq = double.MaxValue;
        foreach (var p in points)
        {
            var dx = (double)p.X - me.X;
            var dy = (double)p.Y - me.Y;
            var sq = dx * dx + dy * dy;
            if (sq < bestSq) { bestSq = sq; best = p; }
        }
        return best is { } b ? (b, (int)Math.Round(Math.Sqrt(bestSq) / 100)) : null;
    }

    private static Data Load()
    {
        var data = new Data([], [], [], []);
        try
        {
            using var doc = JsonDocument.Parse(EmbeddedResources.Read("maps.ru.json"));
            var root = doc.RootElement;
            foreach (var o in root.GetProperty("objects").EnumerateObject())
                if (int.TryParse(o.Name, out var id))
                    data.Objects[id] = new GatherObject(id, o.Value.GetProperty("kind").GetString() ?? "", o.Value.GetProperty("name").GetString() ?? "");
            foreach (var m in root.GetProperty("petOf").EnumerateObject())
                if (int.TryParse(m.Name, out var code)) data.PetOf[code] = m.Value.GetInt32();
            foreach (var p in root.GetProperty("pets").EnumerateObject())
                if (int.TryParse(p.Name, out var id))
                    data.Pets[id] = (p.Value.GetProperty("name").GetString() ?? "", p.Value.GetProperty("category").GetString() ?? "");

            var points = root.GetProperty("points");
            foreach (var m in root.GetProperty("maps").EnumerateArray())
            {
                var id = m.GetProperty("id").GetInt32();
                if (!points.TryGetProperty(id.ToString(), out var pts)) continue;
                var layer = new MapLayer(
                    id,
                    m.GetProperty("name").GetString() ?? $"Карта {id}",
                    pts.GetProperty("bosses").EnumerateArray()
                        .Select(b => new MapBoss(b[0].GetInt32(), new MapPoint(b[1].GetSingle(), b[2].GetSingle()), b[3].GetString() ?? "")).ToList(),
                    Groups(pts.GetProperty("pets")),
                    Groups(pts.GetProperty("gather")),
                    Points(pts.GetProperty("cubes")));
                data.Maps[id] = layer;
                foreach (var alt in m.GetProperty("alt").EnumerateArray()) data.Maps[alt.GetInt32()] = layer;
            }
        }
        catch (Exception)
        {
            // no data: the map window says the map is not known
        }
        return data;
    }

    private static Dictionary<int, IReadOnlyList<MapPoint>> Groups(JsonElement e)
    {
        var groups = new Dictionary<int, IReadOnlyList<MapPoint>>();
        foreach (var g in e.EnumerateObject())
            if (int.TryParse(g.Name, out var id)) groups[id] = Points(g.Value);
        return groups;
    }

    private static List<MapPoint> Points(JsonElement e) =>
        e.EnumerateArray().Select(p => new MapPoint(p[0].GetSingle(), p[1].GetSingle())).ToList();
}
