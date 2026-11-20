// aion2-overlay fork: new file (see FORK_CHANGES.md).
using System.Buffers.Binary;

namespace AionDpsMeter.Timers.Character;

public sealed record WornItem(int Slot, int ItemId, int Enchant);

/// <summary>
/// The full equipment list the game sends at login (frame 11 56). One entry per worn piece: item id (u32), 01 00 00 00,
/// four zero bytes, 0b, slot index; then zeros whose first non-zero byte (20–31 bytes behind the slot) has the enchant
/// level 14 bytes before it. A crafted piece carries its crafter's name behind the slot, which pushes the entry back.
/// Layout from SkeeveAN/Aion-DPS-Meter (MIT, Aion2FrameDecoder.DecodeEquipment), written anew here.
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

            var enchant = 0;
            var nameEnd = slot < ArcanaSlotStart ? CrafterNameEnd(frame, p + 14) : -1;
            var from = nameEnd > 0 ? nameEnd + 14 : p + 14 + 20;
            var to = Math.Min(frame.Length, nameEnd > 0 ? nameEnd + 34 : p + 14 + 31);
            for (var k = from; k < to; k++)
            {
                if (frame[k] == 0) continue;
                enchant = frame[k - 14] <= 30 ? frame[k - 14] : 0;
                break;
            }
            items.Add(new WornItem(slot, itemId, enchant));
        }
        return items.OrderBy(i => i.Slot).ToList();
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
