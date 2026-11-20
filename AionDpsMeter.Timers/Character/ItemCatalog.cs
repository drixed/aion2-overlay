// aion2-overlay fork: new file (see FORK_CHANGES.md).
using System.Text.Json;

namespace AionDpsMeter.Timers.Character;

/// <param name="Slot">MainHand, SubHand, Helmet, Shoulder, Torso, Pants, Gloves, Boots, Cape, Necklace, Earring, Ring,
/// Bracelet, Belt, Amulet, Rune, Arcana…</param>
/// <param name="Grade">1 common … 5 legendary/unique, as the game colours it.</param>
public sealed record ItemInfo(int Id, string Name, string Slot, int Grade, int Tier, int ItemLevel, string? Icon)
{
    /// <summary>The icon on NCSOFT's own public game data CDN (the same one the skill icons come from).</summary>
    public string? IconUrl => string.IsNullOrEmpty(Icon) ? null : $"https://assets.playnccdn.com/static-aion2-gamedata/resources/{Icon}.png";
}

/// <summary>AION 2 items: id → Russian name, slot, grade, tier, item level, icon (Data/items.ru.json; see NOTICE.md).</summary>
public static class ItemCatalog
{
    private static readonly Lazy<Dictionary<int, ItemInfo>> Items = new(Load);

    public static ItemInfo? Find(int id) => Items.Value.GetValueOrDefault(id);

    public static bool Knows(int id) => Items.Value.ContainsKey(id);

    private static Dictionary<int, ItemInfo> Load()
    {
        var items = new Dictionary<int, ItemInfo>();
        try
        {
            using var doc = JsonDocument.Parse(EmbeddedResources.Read("items.ru.json"));
            foreach (var p in doc.RootElement.EnumerateObject())
            {
                if (!int.TryParse(p.Name, out var id) || p.Value.GetArrayLength() < 6) continue;
                var v = p.Value;
                items[id] = new ItemInfo(id, v[0].GetString() ?? "", v[1].GetString() ?? "", v[2].GetInt32(), v[3].GetInt32(), v[4].GetInt32(), v[5].GetString());
            }
        }
        catch (Exception)
        {
            // no catalog: the window shows item ids
        }
        return items;
    }
}
