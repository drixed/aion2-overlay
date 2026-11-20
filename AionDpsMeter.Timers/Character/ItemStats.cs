// aion2-overlay fork: new file (see FORK_CHANGES.md).
using System.Globalization;
using System.Reflection;
using System.Text.Json;

namespace AionDpsMeter.Timers.Character;

/// <summary>
/// Names for what a piece carries (Data/item_stats.ru.json, from SkeeveAN/Aion-DPS-Meter, MIT — see NOTICE.md): stat ids of
/// stones and rolled stats (percent stats come in hundredths), godstones, the base stats of the pieces the table knows, and the
/// arcana Lords (7–17).
/// </summary>
public static class ItemStats
{
    private sealed record Stat(string Name, bool Percent, bool Hundredths);
    private sealed record Data(Dictionary<int, Stat> Stats, Dictionary<int, string> Godstones, Dictionary<int, List<(string Name, long Value)>> Base, Dictionary<int, string> Lords);

    private static readonly Lazy<Data> Loaded = new(Load);

    public static string Name(int stat) => Loaded.Value.Stats.TryGetValue(stat, out var s) ? s.Name : $"Стат {stat}";

    /// <summary>The value as the game shows it: "7.32%" for percent stats, the number otherwise.</summary>
    public static string Value(int stat, long value)
    {
        if (!Loaded.Value.Stats.TryGetValue(stat, out var s)) return value.ToString(CultureInfo.InvariantCulture);
        if (s.Percent) return (value / 100.0).ToString("0.##", CultureInfo.InvariantCulture) + "%";
        if (s.Hundredths) return Math.Round(value / 100.0).ToString(CultureInfo.InvariantCulture);
        return value.ToString(CultureInfo.InvariantCulture);
    }

    public static string? Godstone(int id) => id != 0 && Loaded.Value.Godstones.TryGetValue(id, out var name) ? name : null;

    /// <summary>The piece's own stats (without rolls and stones), empty when the table does not know the piece.</summary>
    public static IReadOnlyList<(string Name, long Value)> Base(int itemId) =>
        Loaded.Value.Base.TryGetValue(itemId, out var list) ? list : [];

    public static string Lord(int stat) => Loaded.Value.Lords.TryGetValue(stat, out var name) ? name : $"Владыка {stat}";

    private static Data Load()
    {
        var empty = new Data([], [], [], []);
        try
        {
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("item_stats.ru.json");
            if (stream is null) return empty;
            using var doc = JsonDocument.Parse(stream);
            var root = doc.RootElement;
            var stats = root.GetProperty("stats").EnumerateObject().ToDictionary(
                p => int.Parse(p.Name, CultureInfo.InvariantCulture),
                p => new Stat(p.Value.GetProperty("n").GetString() ?? p.Name, p.Value.TryGetProperty("p", out _), p.Value.TryGetProperty("h", out _)));
            var godstones = root.GetProperty("godstones").EnumerateObject().ToDictionary(p => int.Parse(p.Name, CultureInfo.InvariantCulture), p => p.Value.GetString() ?? "");
            var bases = root.GetProperty("base").EnumerateObject().ToDictionary(
                p => int.Parse(p.Name, CultureInfo.InvariantCulture),
                p => p.Value.EnumerateArray().Select(e => (e[0].GetString() ?? "", e[1].GetInt64())).ToList());
            var lords = root.GetProperty("lords").EnumerateObject().ToDictionary(p => int.Parse(p.Name, CultureInfo.InvariantCulture), p => p.Value.GetString() ?? "");
            return new Data(stats, godstones, bases, lords);
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or FormatException or InvalidOperationException)
        {
            return empty;
        }
    }
}
