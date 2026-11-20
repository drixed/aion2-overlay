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

    // A piece with its details behind the size byte (layout checked on the EU login list of 2026-11-20): size u32, a flag byte,
    // the stone count, 7 bytes per stone (4 constant bytes, stat u16, tier), the owner block (…, 03), the godstone u32,
    // 9 bytes, then the rolled stats: count, stat u16 + value u32 each; a second list and the skill bonuses follow.
    private static byte[] Detailed(int itemId, byte slot, byte enchant, (int Stat, byte Tier)[] stones, int godstone, (int Stat, int Value)[] stats)
    {
        var e = new List<byte>(Entry(itemId, slot, enchant)[..34]);
        e.AddRange([0x60, 0, 0, 0, 0x00, (byte)stones.Length]);
        foreach (var (stat, tier) in stones) e.AddRange([0x11, 0x22, 0x33, 0x44, (byte)stat, (byte)(stat >> 8), tier]);
        e.AddRange([7, 7, 7, 7, 0, 0, 0x1C, 0x05, 1, 3]);
        e.AddRange(BitConverter.GetBytes(godstone));
        e.AddRange(new byte[9]);
        e.Add((byte)stats.Length);
        foreach (var (stat, value) in stats) { e.AddRange([(byte)stat, (byte)(stat >> 8)]); e.AddRange(BitConverter.GetBytes(value)); }
        e.AddRange([0, 0]);
        return [.. e];
    }

    // An arcana piece: the experience u32 right behind the level, then from the size byte: 01 | god stat | 00 | god value u32
    // at +28, and at +37 two skill lists (count, skill u32 + levels u8): what the piece gives and what it rolled on top.
    private static byte[] Arcana(int itemId, byte slot, byte level, int experience, byte god, int godValue, int skill, byte given, byte rolled)
    {
        var e = Entry(itemId, slot, level)[..34];
        BitConverter.GetBytes(experience).CopyTo(e, 21);
        var tail = new byte[37];
        tail[0] = 0x60;
        tail[28] = 1;
        tail[29] = god;
        BitConverter.GetBytes(godValue).CopyTo(tail, 31);
        return [.. e, .. tail, 1, .. BitConverter.GetBytes(skill), given, 1, .. BitConverter.GetBytes(skill), rolled];
    }

    [Fact]
    public void A_piece_brings_its_stones_rolled_stats_and_godstone()
    {
        var known = new HashSet<int> { 115030053 };
        var item = Assert.Single(EquipmentParser.Parse(Frame(Detailed(115030053, 2, 18, [(317, 2), (104, 1)], 19950001, [(5, 25), (32, 732)])), known.Contains));
        Assert.Equal(18, item.Enchant);
        Assert.Equal([new ItemStone(317, 2), new ItemStone(104, 1)], item.Stones!);
        Assert.Equal([new ItemStat(5, 25), new ItemStat(32, 732)], item.Stats!);
        Assert.Equal(19950001, item.Godstone);
    }

    [Fact]
    public void An_arcana_brings_its_lord_value_and_skill_levels()
    {
        var known = new HashSet<int> { 900001 };
        var item = Assert.Single(EquipmentParser.Parse(Frame(Arcana(900001, 41, 3, 600, 7, 22, 11010000, 1, 1)), known.Contains));
        Assert.Equal(new ArcanaInfo(600, 7, 22, [new SkillLevel(11010000, 2)]), item.Arcana! with { Skills = item.Arcana!.Skills.ToList() }, ArcanaComparer);
    }

    private static readonly IEqualityComparer<ArcanaInfo> ArcanaComparer = EqualityComparer<ArcanaInfo>.Create(
        (a, b) => a!.Experience == b!.Experience && a.GodStat == b.GodStat && a.GodValue == b.GodValue && a.Skills.SequenceEqual(b.Skills));

    [Fact]
    public void Stats_have_russian_names_and_percent_stats_show_as_percent()
    {
        Assert.Equal("7.32%", ItemStats.Value(32, 732));
        Assert.Equal("25", ItemStats.Value(5, 25));
        Assert.DoesNotContain("#", ItemStats.Name(5));
        Assert.Equal("Стат 9999", ItemStats.Name(9999));
        Assert.NotNull(ItemStats.Godstone(19950001));
        Assert.NotEmpty(ItemStats.Base(115030053));
        Assert.Equal("Справедливость", ItemStats.Lord(7));
    }

    // Another player's character window (0x3650, sent when it is opened in the game): 50 36 00 00 07 | name | class code u32
    // (4 * class + 1..4) | 01 | faction | level u32 | 4 zero bytes | gear score u32 | … server u16 + legion name … | per piece: item
    // u32, nine zero bytes, enchant, then the same details block as the login list.
    private static byte[] InspectFrame()
    {
        var f = new List<byte> { 0x50, 0x36, 0, 0, 7, 6 };
        f.AddRange("Tester"u8.ToArray());
        f.AddRange(BitConverter.GetBytes(26));            // Sorcerer: (26 - 1) / 4 = 6
        f.AddRange([1, 2]);
        f.AddRange(BitConverter.GetBytes(45));
        f.AddRange([0, 0, 0, 0]);
        f.AddRange(BitConverter.GetBytes(3110));
        f.AddRange([0x1C, 0x05, 5, .. "Guild"u8.ToArray(), 0, 0]);
        f.AddRange(BitConverter.GetBytes(115030053));
        f.AddRange(new byte[9]);
        f.Add(18);
        f.AddRange(new byte[6]);
        f.AddRange([0x60, 0, 0, 0, 0x00, 1, 0x11, 0x22, 0x33, 0x44, 0x3D, 0x01, 2, 7, 7, 7, 7, 0, 0, 0x1C, 0x05, 1, 3]);
        f.AddRange(BitConverter.GetBytes(19950001));
        f.AddRange(new byte[9]);
        f.AddRange([1, 0x20, 0x00, .. BitConverter.GetBytes(732), 0, 0]);
        f.AddRange(BitConverter.GetBytes(110120003));
        f.AddRange(new byte[9]);
        f.Add(10);
        f.AddRange(new byte[8]);
        return [.. f];
    }

    [Fact]
    public void An_inspected_player_comes_with_class_level_gear_score_and_gear()
    {
        var p = InspectParser.Parse(DateTime.Now, InspectFrame())!;
        Assert.Equal("Tester", p.Name);
        Assert.Equal(15, p.ClassId);                       // our Волшебник
        Assert.Equal(45, p.Level);
        Assert.Equal(3110, p.GearScore);
        Assert.Equal("Guild", p.Guild);
        Assert.Equal(1308, p.Server);
        var weapon = p.Items.Single(i => i.ItemId == 115030053);
        Assert.Equal(18, weapon.Enchant);
        Assert.Equal([new ItemStone(317, 2)], weapon.Stones!);
        Assert.Equal([new ItemStat(32, 732)], weapon.Stats!);
        Assert.Equal(19950001, weapon.Godstone);
        Assert.Equal(10, p.Items.Single(i => i.ItemId == 110120003).Enchant);
        Assert.Null(InspectParser.Parse(DateTime.Now, [0x50, 0x36, 0, 0, 7]));
    }

    [Fact]
    public void Inspected_players_are_kept_by_name_newest_first()
    {
        using var dir = new TempDir();
        var store = new InspectedPlayers(dir.File("inspected.json"));
        store.OnFrame(new DateTime(2026, 11, 20, 17, 0, 40), InspectFrame());
        Assert.Equal("Tester", store.Find("tester")!.Name);
        var again = new InspectedPlayers(dir.File("inspected.json"));
        Assert.Equal(["Tester"], again.All().Select(p => p.Name));
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

    [Fact]
    public void After_a_restart_my_own_row_gets_my_name_from_the_last_login()
    {
        // Nov 2026: after a restart in the dungeon everyone got a name but me ("Player_13189"): the game names you only on entering
        var named = new List<(int Entity, string Name, string? Server, long Power)>();
        var me = new CharacterProfile("DRIXED", 15, 45, "MES", 202780, 3110);
        int? classOf13189 = null;
        var namer = new SelfNamer(() => me, e => e == 13189 ? (false, classOf13189) : null, (e, n, s, p) => named.Add((e, n, s, p)));

        namer.OnLocalEntity(13189);                 // not hit yet: no class known, the name from the last login
        Assert.Equal([(13189, "DRIXED", "MES", 202780L)], named);
        namer.OnLocalEntity(13189);                 // once is enough
        Assert.Single(named);
    }

    [Fact]
    public void Another_class_than_the_last_login_is_not_named()
    {
        // logged in with an alt while the meter was off: a Gladiator is not "DRIXED"
        var named = new List<int>();
        var namer = new SelfNamer(() => new CharacterProfile("DRIXED", 15, 45, "MES", 1, 1), _ => (false, 11), (e, _, _, _) => named.Add(e));
        namer.OnLocalEntity(500);
        Assert.Empty(named);
        var known = new SelfNamer(() => new CharacterProfile("DRIXED", 15, 45, "MES", 1, 1), _ => (true, 15), (e, _, _, _) => named.Add(e));
        known.OnLocalEntity(501);                   // already named by the game: left alone
        Assert.Empty(named);
    }
}

