// aion2-overlay fork: new file (see FORK_CHANGES.md).
using System.Text.Json;
using AionDpsMeter.Services.PacketProcessing.Fork;
using AionDpsMeter.Services.PacketProcessing.Routing;
using AionDpsMeter.Timers.Bosses;

namespace AionDpsMeter.Timers.Character;

/// <summary>Who the character is, as last seen by the meter: kept so the window shows a name and class after a restart.</summary>
public sealed record CharacterProfile(string Name, long ClassId, int Level, string? Server, long CombatPower, long GearScore);

/// <summary>The "Персонаж" window's data: the equipment list the game sends at login, kept in character.json so the window
/// is never empty (it shows the last login).</summary>
public sealed class CharacterTracker(string path) : IRawPacketListener
{
    private const ushort EquipmentOpcode = 0x5611;
    private sealed record Saved(DateTime? At, List<WornItem>? Items, CharacterProfile? Profile = null);

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
                return last is { At: { } at, Items: { } items } ? (at, items) : null;
            }
        }
    }

    public CharacterProfile? Profile
    {
        get
        {
            lock (gate)
            {
                if (!loaded) { loaded = true; last ??= Load(); }
                return last?.Profile;
            }
        }
    }

    /// <summary>The character as the meter sees it now (from the entity marked as me); kept when it is known and new.</summary>
    public void RememberProfile(CharacterProfile profile)
    {
        if (profile.Name.Length == 0) return;
        lock (gate)
        {
            if (!loaded) { loaded = true; last ??= Load(); }
            if (last?.Profile == profile) return;
            last = (last ?? new Saved(null, null)) with { Profile = profile };
            Write();
        }
        Changed?.Invoke();
    }

    private void Write()
    {
        try { File.WriteAllText(path, JsonSerializer.Serialize(last)); } catch (IOException) { }
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
                if (!loaded) { loaded = true; last ??= Load(); }
                last = new Saved(DateTime.Now, items.ToList(), last?.Profile);
                Write();
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
