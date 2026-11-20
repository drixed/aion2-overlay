using AionDpsMeter.Timers.Character;

namespace AionDpsMeter.Timers.Tests.Character;

public class EquipmentParserTests
{
    // One login-list entry: item id (u32 LE), 01 00 00 00, four zero bytes, 0b, slot, zeros with the enchant 14 bytes
    // before the entry's first non-zero "size" byte (20–31 bytes behind the slot).
    private static byte[] Entry(int itemId, byte slot, byte enchant)
    {
        var e = new byte[14 + 21];
        BitConverter.GetBytes(itemId).CopyTo(e, 0);
        e[4] = 1;
        e[12] = 0x0b;
        e[13] = slot;
        e[14 + 6] = enchant;   // 14 before the size byte
        e[14 + 20] = 0x30;     // the size byte
        return e;
    }

    private static byte[] Frame(params byte[][] entries) => [0x11, 0x56, 0x00, .. entries.SelectMany(e => e)];

    [Fact]
    public void The_login_list_gives_each_worn_item_with_its_slot_and_enchant()
    {
        var known = new HashSet<int> { 110120003, 120020001 };
        var items = EquipmentParser.Parse(Frame(Entry(110120003, 1, 10), Entry(120020001, 3, 4), Entry(999, 5, 2)), known.Contains);
        Assert.Equal([(1, 110120003, 10), (3, 120020001, 4)], items.Select(i => (i.Slot, i.ItemId, i.Enchant)));
    }

    [Fact]
    public void A_slot_is_taken_once_and_junk_is_no_item()
    {
        var known = new HashSet<int> { 110120003 };
        Assert.Single(EquipmentParser.Parse(Frame(Entry(110120003, 1, 10), Entry(110120003, 1, 3)), known.Contains));
        Assert.Empty(EquipmentParser.Parse([0x11, 0x56, 1, 2, 3], known.Contains));
    }

    [Fact]
    public void The_catalog_knows_items_with_russian_names()
    {
        var item = ItemCatalog.Find(110120003);
        Assert.NotNull(item);
        Assert.Equal("MainHand", item!.Slot);
        Assert.Equal(98, item.ItemLevel);
        Assert.Contains("Рудры", item.Name);
        Assert.StartsWith("https://assets.playnccdn.com/", item.IconUrl);
    }

    [Fact]
    public void The_character_name_and_class_are_kept_with_the_equipment()
    {
        using var dir = new TempDir();
        var path = dir.File("character.json");
        var tracker = new CharacterTracker(path);
        Assert.Null(tracker.Profile);
        tracker.RememberProfile(new CharacterProfile("andrew", 17, 45, "Месламтаеда", 123456, 2100));
        tracker.RememberProfile(new CharacterProfile("", 0, 0, null, 0, 0));        // nothing known: the old one stays

        var again = new CharacterTracker(path);
        Assert.Equal(new CharacterProfile("andrew", 17, 45, "Месламтаеда", 123456, 2100), again.Profile);
        Assert.Null(again.Equipment);
    }
}
