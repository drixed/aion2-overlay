// aion2-overlay fork: new file (see FORK_CHANGES.md).
using System.Buffers.Binary;
using AionDpsMeter.Services.PacketProcessing.Fork;
using AionDpsMeter.Services.PacketProcessing.Routing;
using AionDpsMeter.Timers.Bosses;

namespace AionDpsMeter.Timers.Farm;

public readonly record struct InventoryRecord(int Kind, int Container, int Slot, int Item, long Quantity);

/// <summary>
/// Inventory records of the player's own frames: the login frame (11 56) lists every container, a change (1B 56) sends one
/// record per slot with its new quantity. A record is found by its anchor: kind byte (0 new, 1 changed, 2 gone) ten bytes
/// before the item id (u32), the quantity (u64, the high three bytes zero), a known container and the slot.
/// Layout from SkeeveAN/Aion-DPS-Meter (MIT, Aion2Stock), written anew here.
/// </summary>
public static class InventoryRecords
{
    private static readonly HashSet<int> Containers = [1, 2, 4, 11, 15, 17];

    public static IReadOnlyList<InventoryRecord> Read(ReadOnlySpan<byte> frame)
    {
        var records = new List<InventoryRecord>();
        var body = frame.Length > 2 ? frame[2..] : default;
        var seen = new HashSet<(int, int)>();
        for (var i = 10; i + 14 <= body.Length; i++)
        {
            var item = unchecked((int)BinaryPrimitives.ReadUInt32LittleEndian(body[i..]));
            var container = body[i + 12];
            if (item < 100_000_000 || body[i - 10] > 2 || !Containers.Contains(container)
                || body[i + 9] != 0 || body[i + 10] != 0 || body[i + 11] != 0 || !FarmIncome.Tracks(item)) continue;
            var quantity = unchecked((long)BinaryPrimitives.ReadUInt64LittleEndian(body[(i + 4)..]));
            int slot = body[i + 13];
            if (seen.Add((container, slot))) records.Add(new InventoryRecord(body[i - 10], container, slot, item, quantity));
            i += 13;
        }
        return records;
    }
}

/// <summary>
/// "Доход с фарма": kina and Abyss points that came in since the meter started (or "сброс"), and per hour. Only what came
/// in counts — spending is not income — and a whole inventory resent after a sort is taken as the new base, not as loot.
/// </summary>
public sealed class FarmIncome : IRawPacketListener
{
    public const int Kina = 930100031, KinaBound = 930100001, AbyssPoints = 930100003;
    private const ushort LoginOpcode = 0x5611, ChangeOpcode = 0x561B;
    private const int InventoryContainer = 1;
    private const int ResendRecords = 10; // a sort resends the whole inventory at once

    private readonly Lock gate = new();
    private readonly Dictionary<int, (int Item, long Quantity)> slots = [];
    private readonly Dictionary<int, long> gained = [];
    private DateTime? since;

    public event Action? Changed;

    public static bool Tracks(int item) => item is Kina or KinaBound or AbyssPoints;

    public long Gained(int item)
    {
        lock (gate) return item == Kina ? gained.GetValueOrDefault(Kina) + gained.GetValueOrDefault(KinaBound) : gained.GetValueOrDefault(item);
    }

    /// <summary>How long the count must run before a rate per hour means something: one boss chest a few minutes after the
    /// start made millions "per hour".</summary>
    public static readonly TimeSpan RateAfter = TimeSpan.FromMinutes(15);

    /// <summary>What came in per hour since the start, null until the count has run <see cref="RateAfter"/>.</summary>
    public double? PerHour(int item, DateTime now)
    {
        DateTime? start;
        lock (gate) start = since;
        if (start is not { } s || now - s < RateAfter) return null;
        return Gained(item) / (now - s).TotalHours;
    }

    public DateTime? Since { get { lock (gate) return since; } }

    public void Reset(DateTime now)
    {
        lock (gate)
        {
            gained.Clear();
            since = now;
        }
        Changed?.Invoke();
    }

    /// <summary>The login list: the base to count from.</summary>
    public void OnSnapshot(IReadOnlyList<InventoryRecord> records, DateTime now)
    {
        lock (gate)
        {
            foreach (var r in records.Where(r => r.Container == InventoryContainer))
                if (r.Kind != 2 && r.Quantity > 0) slots[r.Slot] = (r.Item, r.Quantity);
            since ??= now;
        }
    }

    public void OnChange(IReadOnlyList<InventoryRecord> records, DateTime now)
    {
        var inventory = records.Where(r => r.Container == InventoryContainer).ToList();
        if (inventory.Count == 0) return;
        lock (gate)
        {
            since ??= now;
            var resend = inventory.Count >= ResendRecords;
            foreach (var r in inventory)
            {
                var had = slots.TryGetValue(r.Slot, out var old);
                if (r.Kind == 2 || r.Quantity == 0) { slots.Remove(r.Slot); continue; }
                slots[r.Slot] = (r.Item, r.Quantity);
                if (resend) continue;
                var delta = had && old.Item == r.Item ? r.Quantity - old.Quantity : had || r.Kind != 0 ? 0 : r.Quantity;
                if (delta > 0) gained[r.Item] = gained.GetValueOrDefault(r.Item) + delta;
            }
        }
        Changed?.Invoke();
    }

    public void OnRawPacket(ushort opcode, Packet packet)
    {
        if (opcode is not (LoginOpcode or ChangeOpcode)) return;
        try
        {
            if (!Wire.TryVarint(packet.Data, 0, out _, out var header) || packet.Data.Length < header + 2) return;
            var records = InventoryRecords.Read(packet.Data.AsSpan(header));
            if (opcode == LoginOpcode) OnSnapshot(records, DateTime.Now);
            else OnChange(records, DateTime.Now);
        }
        catch (Exception)
        {
            // an unreadable frame changes nothing
        }
    }
}
