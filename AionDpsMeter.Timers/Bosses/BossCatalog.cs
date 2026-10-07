using System.Text.Json;

namespace AionDpsMeter.Timers.Bosses;

/// <summary>
/// NPC table: which codes are bosses and what they are called. Built from several tables: the first one that knows a
/// code gives its name; later ones only add codes the earlier ones lack (so new bosses from upstream's mobs.json show up).
/// </summary>
public sealed class BossCatalog
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    private readonly Dictionary<int, Npc> npcs;
    private readonly Dictionary<int, List<int>> blocks = new();
    private readonly object gate = new();

    private sealed record Npc(string Name, bool IsBoss, bool IsDummy, int Source);

    private BossCatalog(Dictionary<int, Npc> npcs) => this.npcs = npcs;

    public static BossCatalog Empty { get; } = new(new());

    public static BossCatalog FromJson(params string[] tables)
    {
        var npcs = new Dictionary<int, Npc>();
        for (var source = 0; source < tables.Length; source++)
        {
            Dictionary<string, NpcDto>? table;
            try
            {
                table = JsonSerializer.Deserialize<Dictionary<string, NpcDto>>(tables[source], Options);
            }
            catch (JsonException)
            {
                continue;
            }
            foreach (var (key, dto) in table ?? new())
            {
                if (!int.TryParse(key, out var code) || npcs.ContainsKey(code)) continue;
                npcs[code] = new Npc(dto.Name ?? "", dto.IsBoss, dto.IsDummy, source);
            }
        }
        return new BossCatalog(npcs);
    }

    /// <summary>The Russian table built into the app, then RATmeter's own mobs.json (kept fresh by upstream).</summary>
    public static BossCatalog LoadDefault()
    {
        var tables = new List<string> { EmbeddedResources.Read("npcs.ru.json") };
        var upstreamMobs = Path.Combine(AppContext.BaseDirectory, "GameData", "Assets", "mobs.json");
        try
        {
            if (File.Exists(upstreamMobs)) tables.Add(File.ReadAllText(upstreamMobs));
        }
        catch (IOException) { }
        return FromJson(tables.ToArray());
    }

    public bool IsBoss(int code) => npcs.TryGetValue(code, out var n) && n.IsBoss && !n.IsDummy;

    public string Name(int code) => npcs.TryGetValue(code, out var n) && n.Name.Length > 0 ? n.Name : $"Босс {code}";

    /// <summary>
    /// A map's field bosses share one block of a thousand NPC codes, and the in-game list shows them in code order.
    /// Counted from the first table that has bosses in the block, so a second table cannot shift the places.
    /// </summary>
    public IReadOnlyList<int> FieldBossesInBlock(int block)
    {
        lock (gate)
        {
            if (blocks.TryGetValue(block, out var cached)) return cached;
            var bosses = npcs.Where(kv => kv.Value.IsBoss && !kv.Value.IsDummy && kv.Key / 1000 == block).ToList();
            var source = bosses.Count == 0 ? 0 : bosses.Min(kv => kv.Value.Source);
            var codes = bosses.Where(kv => kv.Value.Source == source).Select(kv => kv.Key).Order().ToList();
            blocks[block] = codes;
            return codes;
        }
    }

    /// <summary>The boss in a list slot (map × 100 + place) when the block holds exactly as many bosses as the list; else 0.</summary>
    public int BossInSlot(int block, int mapId, int slotId, int slotCount)
    {
        var codes = FieldBossesInBlock(block);
        var place = slotId - mapId * 100;
        return codes.Count == slotCount && place >= 1 && place <= codes.Count ? codes[place - 1] : 0;
    }

    internal sealed class NpcDto
    {
        public string? Name { get; set; }
        public bool IsBoss { get; set; }
        public bool IsDummy { get; set; }
    }
}
