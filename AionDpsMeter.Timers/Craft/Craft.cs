// aion2-overlay fork: new file (see FORK_CHANGES.md).
using System.Reflection;
using System.Text.Json;
using AionDpsMeter.Services.PacketProcessing.Fork;
using AionDpsMeter.Services.PacketProcessing.Routing;
using AionDpsMeter.Timers.Bosses;
using AionDpsMeter.Timers.Farm;

namespace AionDpsMeter.Timers.Craft;

/// <summary>One material of a recipe: the item, how many a craft uses, and what the game lets the player use instead.</summary>
public sealed record CraftComponent(int Item, int Count, IReadOnlyList<(int Item, int Count)> Alternatives);

/// <summary>A crafting recipe of the game (one per faction). "Combo" marks a hidden one that makes a material of another recipe.</summary>
public sealed record CraftRecipe(int Id, bool Elyos, string Category, int Level, string Learn, int Product, int Count, IReadOnlyList<CraftComponent> Components)
{
    public bool Hidden => Learn == "Combo";
}

/// <summary>
/// The recipes and the Russian names of what they make and use (Data/craft.ru.json, from SkeeveAN/Aion-DPS-Meter, MIT — see NOTICE.md).
/// </summary>
public sealed class CraftBook
{
    private readonly Dictionary<int, CraftRecipe> byId;
    private readonly Dictionary<(int Product, bool Elyos), CraftRecipe> producers = [];
    private readonly IReadOnlyDictionary<int, (int Grade, string Name)> names;

    public CraftBook(IReadOnlyList<CraftRecipe> recipes, IReadOnlyDictionary<int, (int Grade, string Name)> names)
    {
        Recipes = recipes;
        this.names = names;
        byId = recipes.ToDictionary(r => r.Id);
        // several recipes can make one item (1 gem → 3 dust or 10 → 30): the one needing least per piece, then the lower level, wins
        foreach (var group in recipes.GroupBy(r => (r.Product, r.Elyos)))
            producers[group.Key] = group.OrderBy(r => (double)r.Components.Sum(c => c.Count) / Math.Max(1, r.Count)).ThenBy(r => r.Level).ThenBy(r => r.Id).First();
    }

    private static readonly Lazy<CraftBook> Loaded = new(Load);

    public static CraftBook Default => Loaded.Value;

    public IReadOnlyList<CraftRecipe> Recipes { get; }

    public CraftRecipe? Find(int id) => byId.GetValueOrDefault(id);

    /// <summary>The recipe that makes an item for a faction, null for a raw material.</summary>
    public CraftRecipe? ProducerOf(int item, bool elyos) => producers.GetValueOrDefault((item, elyos));

    public bool Knows(int item) => names.ContainsKey(item);

    public string Name(int item) => names.TryGetValue(item, out var n) ? n.Name : $"Предмет {item}";

    public int Grade(int item) => names.TryGetValue(item, out var n) ? n.Grade : 0;

    private static CraftBook Load()
    {
        var recipes = new List<CraftRecipe>();
        var names = new Dictionary<int, (int, string)>();
        try
        {
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("craft.ru.json");
            if (stream is not null)
            {
                using var doc = JsonDocument.Parse(stream);
                foreach (var r in doc.RootElement.GetProperty("recipes").EnumerateArray())
                {
                    var components = r.GetProperty("k").EnumerateArray().Select(k => new CraftComponent(k[0].GetInt32(), k[1].GetInt32(),
                        k.GetArrayLength() > 2 ? k[2].EnumerateArray().Select(a => (a[0].GetInt32(), a[1].GetInt32())).ToList() : [])).ToList();
                    recipes.Add(new CraftRecipe(r.GetProperty("id").GetInt32(), r.GetProperty("e").GetInt32() == 1, r.GetProperty("cat").GetString() ?? "",
                        r.GetProperty("lv").GetInt32(), r.GetProperty("learn").GetString() ?? "", r.GetProperty("p").GetInt32(), r.GetProperty("n").GetInt32(), components));
                }
                foreach (var p in doc.RootElement.GetProperty("names").EnumerateObject())
                    names[int.Parse(p.Name)] = (p.Value[0].GetInt32(), p.Value[1].GetString() ?? "");
            }
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException or FormatException) { }
        return new CraftBook(recipes, names);
    }
}

/// <summary>A line of the plan: a recipe and how often it is to be crafted.</summary>
public sealed record CraftEntry(int Recipe, int Count);

/// <summary>A material: how many the plan needs and how many of them the stock covers.</summary>
public sealed record CraftLine(int Item, long Needed, long Have)
{
    public long Missing => Math.Max(0, Needed - Have);
}

/// <summary>What one recipe of the plan needs: raw materials (intermediates resolved) and what has to be crafted first.</summary>
public sealed record CraftResult(CraftEntry Entry, CraftRecipe Recipe, IReadOnlyList<CraftLine> Materials, IReadOnlyList<(int Item, long Crafts)> Intermediates)
{
    /// <summary>How much of the material is there, 0 to 100.</summary>
    public int Percent
    {
        get
        {
            var need = Materials.Sum(m => m.Needed);
            return need == 0 ? 100 : (int)(100 * Materials.Sum(m => Math.Min(m.Have, m.Needed)) / need);
        }
    }
}

/// <summary>
/// The materials a plan needs against what the player has, one recipe after the other against the same stock. An intermediate (an ingot,
/// dust) comes from the stock first and the rest from its own recipe in whole crafts, so the list shows the ore, not the ingots.
/// Ported from SkeeveAN/Aion-DPS-Meter (MIT, Aion2Craft.CraftCalc), written anew here.
/// </summary>
public static class CraftCalc
{
    private const int MaxDepth = 6;

    public static List<CraftResult> Compute(CraftBook book, IEnumerable<CraftEntry> entries, IReadOnlyDictionary<int, long> stock, bool resolve, bool elyos)
    {
        var spare = new Dictionary<int, long>(stock);
        var results = new List<CraftResult>();
        foreach (var entry in entries)
        {
            if (entry.Count <= 0 || book.Find(entry.Recipe) is not { } recipe) continue;
            var need = new Dictionary<int, long>();
            var made = new Dictionary<int, long>();
            var covered = new Dictionary<int, long>();

            void Demand(CraftRecipe r, long times, int depth)
            {
                foreach (var c in r.Components)
                {
                    var wanted = times * c.Count;
                    var producer = resolve && depth < MaxDepth ? book.ProducerOf(c.Item, r.Elyos) : null;
                    if (producer is null || producer.Id == r.Id)
                    {
                        need[c.Item] = need.GetValueOrDefault(c.Item) + wanted;
                        continue;
                    }
                    var fromStock = Math.Min(spare.GetValueOrDefault(c.Item), wanted);
                    spare[c.Item] = spare.GetValueOrDefault(c.Item) - fromStock;
                    if (fromStock > 0) covered[c.Item] = covered.GetValueOrDefault(c.Item) + fromStock;
                    var rest = wanted - fromStock;
                    if (rest <= 0) continue;
                    var batches = (rest + producer.Count - 1) / producer.Count;
                    made[c.Item] = made.GetValueOrDefault(c.Item) + batches;
                    Demand(producer, batches, depth + 1);
                }
            }

            Demand(recipe, entry.Count, 0);
            var lines = covered.Select(c => new CraftLine(c.Key, c.Value, c.Value)).ToList();
            foreach (var (item, needed) in need)
            {
                var have = Math.Min(spare.GetValueOrDefault(item), needed);
                spare[item] = spare.GetValueOrDefault(item) - have;
                lines.Add(new CraftLine(item, needed, have));
            }
            results.Add(new CraftResult(entry, recipe, lines, made.Select(m => (m.Key, m.Value)).ToList()));
        }
        return results;
    }

    /// <summary>Every material of the plan summed, the ones missing most first.</summary>
    public static List<CraftLine> Total(IEnumerable<CraftResult> results) =>
        results.SelectMany(r => r.Materials).GroupBy(l => l.Item)
            .Select(g => new CraftLine(g.Key, g.Sum(l => l.Needed), g.Sum(l => l.Have)))
            .OrderByDescending(l => l.Missing).ThenBy(l => l.Item).ToList();
}

/// <summary>
/// What the player owns in the inventory (container 1), the character storage (15) and the account storage (2): all three come in the login
/// list (11 56, EU login 2026-11-20: 70 / 21 / 73 slots); later inventory changes in 1B 56. Moving between the inventory and a storage sends
/// nothing readable, so the storages are as of the login.
/// </summary>
public sealed class CraftStock(Func<int, bool> known) : IRawPacketListener
{
    private const ushort LoginOpcode = 0x5611, ChangeOpcode = 0x561B;
    private static readonly int[] Kept = [1, 15, 2];
    private readonly Lock gate = new();
    private readonly Dictionary<(int Container, int Slot), (int Item, long Quantity)> slots = [];

    public event Action? Changed;

    /// <summary>When the login list was read; null before the first login the meter saw.</summary>
    public DateTime? LoginAt { get; private set; }

    public long Count(int item)
    {
        lock (gate) return slots.Values.Where(s => s.Item == item).Sum(s => s.Quantity);
    }

    public Dictionary<int, long> All()
    {
        lock (gate) return slots.Values.GroupBy(s => s.Item).ToDictionary(g => g.Key, g => g.Sum(s => s.Quantity));
    }

    public void OnRawPacket(ushort opcode, Packet packet)
    {
        if (opcode is not (LoginOpcode or ChangeOpcode)) return;
        try
        {
            if (!Wire.TryVarint(packet.Data, 0, out _, out var header) || packet.Data.Length < header + 3) return;
            OnFrame(DateTime.Now, packet.Data.AsSpan(header));
        }
        catch (Exception) { }
    }

    /// <param name="frame">The frame from its opcode on.</param>
    public void OnFrame(DateTime at, ReadOnlySpan<byte> frame)
    {
        if (frame.Length < 2) return;
        var opcode = (ushort)(frame[0] | frame[1] << 8);
        if (opcode == LoginOpcode && frame.Length < 5000) return;   // the login list is big; a short frame of that opcode is something else
        var records = InventoryRecords.Read(frame, known).Where(r => Kept.Contains(r.Container)).ToList();
        if (records.Count == 0) return;
        lock (gate)
        {
            if (opcode == LoginOpcode)
            {
                slots.Clear();
                LoginAt = at;
            }
            foreach (var r in records)
            {
                if (r.Kind == 2 || r.Quantity == 0) slots.Remove((r.Container, r.Slot));
                else slots[(r.Container, r.Slot)] = (r.Item, r.Quantity);
            }
        }
        Changed?.Invoke();
    }
}

/// <summary>The recipes the player marked and how often, kept in craft-plan.json in the order they were added.</summary>
public sealed class CraftPlan(string path)
{
    private readonly Lock gate = new();
    private List<CraftEntry>? entries;

    public event Action? Changed;

    public IReadOnlyList<CraftEntry> Entries
    {
        get { lock (gate) return [.. List()]; }
    }

    /// <summary>Sets how often a recipe is to be crafted; 0 takes it off the plan.</summary>
    public void Set(int recipe, int count)
    {
        lock (gate)
        {
            var list = List();
            var at = list.FindIndex(e => e.Recipe == recipe);
            if (count <= 0) { if (at >= 0) list.RemoveAt(at); }
            else if (at >= 0) list[at] = new CraftEntry(recipe, count);
            else list.Add(new CraftEntry(recipe, count));
            try { File.WriteAllText(path, JsonSerializer.Serialize(list)); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
        Changed?.Invoke();
    }

    private List<CraftEntry> List()
    {
        if (entries is not null) return entries;
        try { entries = File.Exists(path) ? JsonSerializer.Deserialize<List<CraftEntry>>(File.ReadAllText(path)) : null; }
        catch (Exception) { entries = null; }
        return entries ??= [];
    }
}
