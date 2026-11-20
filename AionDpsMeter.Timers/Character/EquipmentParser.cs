// aion2-overlay fork: new file (see FORK_CHANGES.md).
using System.Buffers.Binary;

namespace AionDpsMeter.Timers.Character;

public sealed record WornItem(int Slot, int ItemId, int Enchant, IReadOnlyList<ItemStone>? Stones = null, IReadOnlyList<ItemStat>? Stats = null,
    int Godstone = 0, ArcanaInfo? Arcana = null);

/// <summary>A mana stone set into a piece: the stat it adds and its tier (1 white, 2 green, 3 blue, 4 gold); 0/0 = an empty slot.</summary>
public sealed record ItemStone(int Stat, int Tier);

/// <summary>A stat rolled on a piece; percent stats come in hundredths (732 = 7.32 %).</summary>
public sealed record ItemStat(int Stat, long Value);

public sealed record SkillLevel(int Skill, int Level);

/// <summary>An arcana piece: experience collected, its Lord (stat 7–17) with the value, and the skill levels it gives (rolled ones added).</summary>
public sealed record ArcanaInfo(int Experience, int GodStat, int GodValue, IReadOnlyList<SkillLevel> Skills);

/// <summary>
/// The full equipment list the game sends at login (frame 11 56). One entry per worn piece: item id (u32), 01 00 00 00,
/// four zero bytes, 0b, slot index; then zeros whose first non-zero byte (20–31 bytes behind the slot) has the enchant
/// level 14 bytes before it. A crafted piece carries its crafter's name behind the slot, which pushes the entry back.
/// Behind the size byte come the stones, the godstone and the rolled stats (arcana: the Lord and skill levels).
/// Layout from SkeeveAN/Aion-DPS-Meter (MIT, Aion2FrameDecoder.DecodeEquipment, TryReadItemDetails, TryReadArcana), written anew here.
/// </summary>
public static class EquipmentParser
{
    /// <summary>Slots from here on are arcana pieces.</summary>
    public const int ArcanaSlotStart = 30;

    /// <param name="frame">The frame from its opcode on.</param>
    /// <param name="knownItem">Only items the catalog knows count: other byte runs that look like an entry are junk.</param>
    public static IReadOnlyList<WornItem> Parse(ReadOnlySpan<byte> frame, Func<int, bool> knownItem)
    {
        var items = new List<WornItem>();
        var seen = new HashSet<int>();
        for (var p = 2; p + 14 < frame.Length; p++)
        {
            if (frame[p + 4] != 1 || frame[p + 5] != 0 || frame[p + 6] != 0 || frame[p + 7] != 0 || frame[p + 12] != 0x0b) continue;
            var itemId = unchecked((int)BinaryPrimitives.ReadUInt32LittleEndian(frame[p..]));
            int slot = frame[p + 13];
            if (!knownItem(itemId) || !seen.Add(slot)) continue;

            var item = new WornItem(slot, itemId, 0);
            var nameEnd = slot < ArcanaSlotStart ? CrafterNameEnd(frame, p + 14) : -1;
            var from = nameEnd > 0 ? nameEnd + 14 : p + 14 + 20;
            var to = Math.Min(frame.Length, nameEnd > 0 ? nameEnd + 34 : p + 14 + 31);
            for (var k = from; k < to; k++)
            {
                if (frame[k] == 0) continue;
                var details = slot < ArcanaSlotStart ? Details(frame, k) : null;
                if (nameEnd > 0 && details is null) continue;   // behind a crafter's name only a block that reads as details counts
                item = item with { Enchant = frame[k - 14] <= 30 ? frame[k - 14] : 0 };
                if (details is { } d) item = item with { Stones = d.Stones, Stats = d.Stats, Godstone = d.Godstone };
                if (slot >= ArcanaSlotStart) item = item with { Arcana = Arcana(frame, k) };
                break;
            }
            items.Add(item);
        }
        return items.OrderBy(i => i.Slot).ToList();
    }

    /// <summary>
    /// From the size byte: size u32, up to two flag bytes, the stone count (1–6) and 7 bytes per stone (4 bytes the same for all
    /// stones of the piece, stat u16, tier ≤ 4), the owner block of 10 bytes ending in 03, the godstone u32 and 9 more bytes,
    /// then the rolled stats (count ≤ 8, stat u16 + value u32). Null when the block does not look like that.
    /// </summary>
    internal static (IReadOnlyList<ItemStone> Stones, IReadOnlyList<ItemStat>? Stats, int Godstone)? Details(ReadOnlySpan<byte> frame, int size)
    {
        if (size + 8 > frame.Length || frame[size + 1] != 0 || frame[size + 2] != 0 || frame[size + 3] != 0) return null;
        for (var r = size + 4; r <= size + 6 && r + 1 < frame.Length; r++)
        {
            int count = frame[r + 1];
            var first = r + 2;
            var end = first + count * 7;
            if (count is < 1 or > 6 || end + 10 > frame.Length || frame[end + 9] != 3) continue;
            var same = true;
            for (var i = 0; i < count && same; i++)
                same = frame[first + i * 7 + 6] <= 4 && frame.Slice(first + i * 7, 4).SequenceEqual(frame.Slice(first, 4));
            if (!same) continue;

            var stones = new List<ItemStone>(count);
            for (var i = 0; i < count; i++)
                stones.Add(new ItemStone(frame[first + i * 7 + 4] | frame[first + i * 7 + 5] << 8, frame[first + i * 7 + 6]));
            var godstone = end + 14 <= frame.Length ? unchecked((int)BinaryPrimitives.ReadUInt32LittleEndian(frame[(end + 10)..])) : 0;
            return (stones, StatList(frame, end + 23), godstone);
        }
        return null;
    }

    private static List<ItemStat>? StatList(ReadOnlySpan<byte> frame, int at)
    {
        if (at >= frame.Length || frame[at] > 8 || at + 1 + frame[at] * 6 > frame.Length) return null;
        var stats = new List<ItemStat>(frame[at]);
        for (var i = 0; i < frame[at]; i++)
        {
            var o = at + 1 + i * 6;
            stats.Add(new ItemStat(frame[o] | frame[o + 1] << 8, BinaryPrimitives.ReadUInt32LittleEndian(frame[(o + 2)..])));
        }
        return stats;
    }

    /// <summary>
    /// An arcana piece: experience u32 right behind the level byte (size - 13); from the size byte <c>01 | Lord | 00 | value u32</c>
    /// at +28 and two skill lists at +37 (count ≤ 8, skill u32 + levels u8: given, then rolled on top). Some entries carry one more
    /// zero byte behind the size field, which shifts the rest by one; other shifts count only with a real Lord (7–17).
    /// </summary>
    private static ArcanaInfo? Arcana(ReadOnlySpan<byte> frame, int size)
    {
        var first = size + 5 < frame.Length && frame[size + 4] == 0 ? 1 : 0;
        if (ArcanaAt(frame, size, first, strict: false) is { } found) return found;
        for (var shift = 0; shift <= 3; shift++)
            if (shift != first && ArcanaAt(frame, size, shift, strict: true) is { } other) return other;
        return null;
    }

    private static ArcanaInfo? ArcanaAt(ReadOnlySpan<byte> frame, int size, int shift, bool strict)
    {
        if (size < 14 || size + 38 + shift > frame.Length || frame[size + 1] != 0 || frame[size + 2] != 0 || frame[size + 3] != 0 || frame[size + 28 + shift] != 1) return null;
        int god = frame[size + 29 + shift];
        if (strict && god is < 7 or > 17) return null;
        var experience = unchecked((int)BinaryPrimitives.ReadUInt32LittleEndian(frame[(size - 13)..]));
        var value = unchecked((int)BinaryPrimitives.ReadUInt32LittleEndian(frame[(size + 31 + shift)..]));
        var at = size + 37 + shift;
        if (Skills(frame, ref at) is not { } given || Skills(frame, ref at) is not { } rolled) return null;
        var sum = given.Select(g => new SkillLevel(g.Skill, g.Level + rolled.Where(r => r.Skill == g.Skill).Sum(r => r.Level))).ToList();
        return new ArcanaInfo(experience, god, value, sum);
    }

    private static List<SkillLevel>? Skills(ReadOnlySpan<byte> frame, ref int at)
    {
        if (at >= frame.Length || frame[at] > 8 || at + 1 + frame[at] * 5 > frame.Length) return null;
        var list = new List<SkillLevel>(frame[at]);
        for (var i = 0; i < frame[at]; i++)
        {
            var o = at + 1 + i * 5;
            list.Add(new SkillLevel(unchecked((int)BinaryPrimitives.ReadUInt32LittleEndian(frame[o..])), frame[o + 4]));
        }
        at += 1 + frame[at] * 5;
        return list;
    }

    /// <summary>The end of a crafter's name (length-prefixed, behind a zero byte, within 8 bytes of the slot), or -1.</summary>
    private static int CrafterNameEnd(ReadOnlySpan<byte> frame, int from)
    {
        for (var o = 0; o < 8 && from + o < frame.Length; o++)
        {
            int length = frame[from + o];
            if (length is < 2 or > 24 || from + o + 1 + length > frame.Length || (o > 0 && frame[from + o - 1] != 0)) continue;
            var text = true;
            for (var i = 0; i < length && text; i++) text = frame[from + o + 1 + i] >= 0x20;
            if (text) return from + o + 1 + length;
        }
        return -1;
    }
}
