// aion2-overlay fork: new file (see FORK_CHANGES.md).
using System.Text.Json;
using AionDpsMeter.Services.PacketProcessing.Fork;
using AionDpsMeter.Services.PacketProcessing.Routing;
using AionDpsMeter.Timers.Bosses;

namespace AionDpsMeter.Timers.Character;

/// <summary>The "Персонаж" window's data: the equipment list the game sends at login, kept in character.json so the window
/// is never empty (it shows the last login).</summary>
public sealed class CharacterTracker(string path) : IRawPacketListener
{
    private const ushort EquipmentOpcode = 0x5611;
    private sealed record Saved(DateTime At, List<WornItem> Items);

    private readonly Lock gate = new();
    private Saved? last;
    private bool loaded;

    public event Action? Changed;

    public (DateTime At, IReadOnlyList<WornItem> Items)? Equipment
    {
        get
        {
            lock (gate)
            {
                if (!loaded) { loaded = true; last ??= Load(); }
                return last is { } s ? (s.At, s.Items) : null;
            }
        }
    }

    public void OnRawPacket(ushort opcode, Packet packet)
    {
        if (opcode != EquipmentOpcode) return;
        try
        {
            if (!Wire.TryVarint(packet.Data, 0, out _, out var header) || packet.Data.Length < header + 2) return;
            var items = EquipmentParser.Parse(packet.Data.AsSpan(header), ItemCatalog.Knows);
            if (items.Count < 3) return; // a partial frame, not the login list
            lock (gate)
            {
                loaded = true;
                last = new Saved(DateTime.Now, items.ToList());
                try { File.WriteAllText(path, JsonSerializer.Serialize(last)); } catch (IOException) { }
            }
            Changed?.Invoke();
        }
        catch (Exception)
        {
            // a frame we cannot read leaves the last equipment shown
        }
    }

    private Saved? Load()
    {
        try { return File.Exists(path) ? JsonSerializer.Deserialize<Saved>(File.ReadAllText(path)) : null; }
        catch (Exception) { return null; }
    }
}
