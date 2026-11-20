// aion2-overlay fork: new file (see FORK_CHANGES.md).
using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
using AionDpsMeter.Services.PacketProcessing.Fork;
using AionDpsMeter.Services.PacketProcessing.Routing;
using AionDpsMeter.Timers.Bosses;

namespace AionDpsMeter.Timers.Character;

/// <summary>Another player as their character window showed them: class (our ids 11–18), level, gear score, legion, server, gear.</summary>
public sealed record InspectedPlayer(string Name, int ClassId, int Level, int GearScore, string? Guild, int Server, IReadOnlyList<WornItem> Items, DateTime At);

/// <summary>
/// Another player's character window, sent when it is opened in the game (0x3650; EU capture 2026-11-20, two players):
/// <c>50 36 00 00 07 | name (u8 length + UTF-8) | class code u32 (4 * class + 1..4) | 01 | faction | level u32 | 4 zero bytes |
/// gear score u32 | … server u16 + legion name … | per piece: item u32, nine zero bytes, enchant (or a 9c/1c marker, then the enchant),
/// then the same details block as the login list</c>. The piece does not say its slot: it follows from the kind of item.
/// Layout from SkeeveAN/Aion-DPS-Meter (MIT, Aion2FrameDecoder.DecodeInspect, Aion2SkillNames.ClassFromCode), written anew here.
/// </summary>
public static class InspectParser
{
    public const ushort Opcode = 0x3650;

    // the class order of the code (Gladiator, Templar, Ranger, Assassin, Spiritmaster, Sorcerer, Cleric, Chanter) in our class ids
    private static readonly int[] ClassIds = [11, 12, 14, 13, 16, 15, 17, 18];

    private static readonly Dictionary<string, int[]> Slots = new()
    {
        ["MainHand"] = [1], ["SubHand"] = [2], ["Helmet"] = [3], ["Shoulder"] = [4], ["Torso"] = [5], ["Pants"] = [6], ["Gloves"] = [7],
        ["Boots"] = [8], ["Necklace"] = [10], ["Earring"] = [11, 12], ["Ring"] = [13, 14], ["Bracelet"] = [15, 16], ["Belt"] = [17],
        ["Cape"] = [19], ["Amulet"] = [22], ["Rune"] = [23, 24],
    };

    /// <summary>The class in a roster or window code (4 * class + 1..4) as our class id, 0 when unknown.</summary>
    public static int ClassOf(int code) => code >= 1 && (code - 1) / 4 is var i and >= 1 and <= 8 ? ClassIds[i - 1] : 0;

    /// <param name="frame">The frame from its opcode on.</param>
    public static InspectedPlayer? Parse(DateTime at, ReadOnlySpan<byte> frame)
    {
        if (frame.Length < 40 || frame[0] != 0x50 || frame[1] != 0x36 || !TryName(frame, 5, out var name)) return null;
        var after = 6 + frame[5];
        if (after + 18 > frame.Length) return null;
        var code = (int)BinaryPrimitives.ReadUInt32LittleEndian(frame[after..]);
        var level = (int)BinaryPrimitives.ReadUInt32LittleEndian(frame[(after + 6)..]);
        var gearScore = (int)BinaryPrimitives.ReadUInt32LittleEndian(frame[(after + 14)..]);
        if (ClassOf(code) == 0 || level is < 1 or > 200) return null;

        string? guild = null;
        var server = 0;
        for (var i = after + 18; i + 4 < Math.Min(frame.Length, after + 80); i++)
        {
            var id = frame[i] | frame[i + 1] << 8;
            if (id is >= 1000 and <= 3000 && TryName(frame, i + 2, out var candidate) && candidate != name)
            {
                guild = candidate;
                if (id % 100 is >= 1 and <= 30) server = id;
                break;
            }
        }

        var items = new List<WornItem>();
        var used = new Dictionary<string, int>();
        var extra = 25;
        var arcanaSlot = EquipmentParser.ArcanaSlotStart + 11;
        for (var q = after + 18; q + 16 <= frame.Length; q++)
        {
            var itemId = unchecked((int)BinaryPrimitives.ReadUInt32LittleEndian(frame[q..]));
            if (ItemCatalog.Find(itemId) is not { } info || !Zeros(frame, q + 4, 9)) continue;
            var shift = frame[q + 13] is 0x9c or 0x1c ? 1 : 0;
            int enchant = frame[q + 13 + shift];
            if (enchant > 30) continue;

            (IReadOnlyList<ItemStone> Stones, IReadOnlyList<ItemStat>? Stats, int Godstone)? details = null;
            if (info.Slot != "Arcana")
            {
                for (var k = q + 14 + shift; k < Math.Min(frame.Length, q + 14 + shift + 20); k++)
                {
                    if (frame[k] == 0) continue;
                    details = EquipmentParser.Details(frame, k);
                    break;
                }
            }

            int slot;
            if (info.Slot == "Arcana") slot = arcanaSlot++;
            else
            {
                var n = used.GetValueOrDefault(info.Slot);
                slot = Slots.TryGetValue(info.Slot, out var slots) && n < slots.Length ? slots[n] : extra++;
                used[info.Slot] = n + 1;
            }
            items.Add(new WornItem(slot, itemId, enchant, details?.Stones, details?.Stats, details?.Godstone ?? 0));
            q += 12;
        }
        return items.Count == 0 ? null : new InspectedPlayer(name, ClassOf(code), level, gearScore, guild, server, items.OrderBy(i => i.Slot).ToList(), at);
    }

    private static bool Zeros(ReadOnlySpan<byte> b, int from, int count)
    {
        if (from + count > b.Length) return false;
        for (var i = 0; i < count; i++) if (b[from + i] != 0) return false;
        return true;
    }

    private static bool TryName(ReadOnlySpan<byte> b, int at, out string name)
    {
        name = string.Empty;
        if (at >= b.Length) return false;
        int length = b[at];
        if (length is < 2 or > 48 || at + 1 + length > b.Length) return false;
        try { name = new UTF8Encoding(false, true).GetString(b.Slice(at + 1, length)); }
        catch (ArgumentException) { return false; }
        return !name.Any(char.IsControl) && name.Trim().Length > 0;
    }
}

/// <summary>The players whose character window was opened in the game, newest first, kept in inspected.json (the last 50).</summary>
public sealed class InspectedPlayers(string path) : IRawPacketListener
{
    private const int Keep = 50;
    private readonly Lock gate = new();
    private List<InspectedPlayer>? players;

    public event Action? Changed;

    public IReadOnlyList<InspectedPlayer> All()
    {
        lock (gate) return [.. Players()];
    }

    public InspectedPlayer? Find(string name)
    {
        lock (gate) return Players().FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    public void OnRawPacket(ushort opcode, Packet packet)
    {
        if (opcode != InspectParser.Opcode) return;
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
        if (InspectParser.Parse(at, frame) is not { } player) return;
        lock (gate)
        {
            var list = Players();
            list.RemoveAll(p => string.Equals(p.Name, player.Name, StringComparison.OrdinalIgnoreCase));
            list.Insert(0, player);
            if (list.Count > Keep) list.RemoveRange(Keep, list.Count - Keep);
            try { File.WriteAllText(path, JsonSerializer.Serialize(list)); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
        Changed?.Invoke();
    }

    private List<InspectedPlayer> Players()
    {
        if (players is not null) return players;
        try { players = File.Exists(path) ? JsonSerializer.Deserialize<List<InspectedPlayer>>(File.ReadAllText(path)) : null; }
        catch (Exception) { players = null; }
        return players ??= [];
    }
}
