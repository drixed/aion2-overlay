using System.Buffers.Binary;

namespace AionDpsMeter.Timers.Bosses;

/// <param name="SlotId">Map id × 100 + the boss's place in the map's list (bosses sorted by NPC code).</param>
/// <param name="AtMs">Unix ms: alive since (alive) / back at (dead); 0 when the game keeps no time.</param>
public sealed record FieldBossSlot(int SlotId, bool Alive, long AtMs, float X, float Y, float Z);

public sealed record FieldBossList(int MapId, int Count, IReadOnlyList<FieldBossSlot> Slots)
{
    public bool Complete => Slots.Count == Count;

    public bool SameAs(FieldBossList other) =>
        MapId == other.MapId && Count == other.Count && Slots.SequenceEqual(other.Slots);
}

/// <summary>
/// The in-game map's field boss list (wire bytes <c>01 91</c>), sent about once a second while the map is open.
/// Layout ported from cyberbadger6969/aion2-dps-meter (GPL-3.0):
/// <c>u16 0, map u32, count u8, count × { alive u8, slot varint, [alive: x y z f32], [u8 on some slots], time i64 ms }, 00…</c>
/// The optional byte is resolved by reading on: only one of the two readings leaves the next slot where it must be.
/// </summary>
public static class FieldBossListParser
{
    /// <summary>RATmeter reads opcodes little-endian: wire bytes 01 91 → 0x9101.</summary>
    public const ushort Opcode = 0x9101;

    public static FieldBossList? Parse(ReadOnlySpan<byte> b)
    {
        if (b.Length < 7) return null;
        var map = (int)BinaryPrimitives.ReadUInt32LittleEndian(b.Slice(2, 4));
        if (map <= 0) return null;
        int count = b[6];
        var slots = new List<FieldBossSlot>(count);
        var o = 7;
        for (var n = 0; n < count && TrySlot(b, ref o, map, last: n == count - 1, out var slot); n++)
            slots.Add(slot);
        return new FieldBossList(map, count, slots);
    }

    private static bool TrySlot(ReadOnlySpan<byte> b, ref int o, int map, bool last, out FieldBossSlot slot)
    {
        slot = null!;
        if (!SlotHeader(b, o, map, out var alive, out var id, out var at)) return false;
        float x = 0, y = 0, z = 0;
        if (alive)
        {
            if (at + 12 > b.Length) return false;
            x = BinaryPrimitives.ReadSingleLittleEndian(b.Slice(at, 4));
            y = BinaryPrimitives.ReadSingleLittleEndian(b.Slice(at + 4, 4));
            z = BinaryPrimitives.ReadSingleLittleEndian(b.Slice(at + 8, 4));
            at += 12;
        }
        // The last slot is normally followed by zero padding, but the EU client may append another block
        // (00 03 …): when no reading ends in padding, the first plausible time wins.
        var passes = last ? 2 : 1;
        for (var pass = 0; pass < passes; pass++)
        {
            for (var extra = 0; extra <= 1; extra++)
            {
                var t = at + extra;
                if (t + 8 > b.Length) break;
                var time = BinaryPrimitives.ReadInt64LittleEndian(b.Slice(t, 8));
                if (!PlausibleTime(time)) continue;
                var end = t + 8;
                var fits = pass == 1
                    || (last
                        ? b[end..].IndexOfAnyExcept((byte)0) < 0 && b.Length - end <= 8
                        : SlotHeader(b, end, map, out _, out _, out _));
                if (!fits) continue;
                slot = new FieldBossSlot(id, alive, time, x, y, z);
                o = end;
                return true;
            }
        }
        return false;
    }

    private static bool PlausibleTime(long ms) => ms == 0 || ms is >= 1_600_000_000_000 and <= 2_600_000_000_000;

    /// <summary><c>alive u8 (0 / 1), slot varint</c> with the slot inside this map's range.</summary>
    private static bool SlotHeader(ReadOnlySpan<byte> b, int o, int map, out bool alive, out int slot, out int next)
    {
        alive = false;
        slot = 0;
        next = o;
        if (o >= b.Length || b[o] > 1) return false;
        alive = b[o] == 1;
        if (!Wire.TryVarint(b, o + 1, out var v, out var len)) return false;
        if (v <= (long)map * 100 || v >= (long)map * 100 + 100) return false;
        slot = (int)v;
        next = o + 1 + len;
        return true;
    }
}
