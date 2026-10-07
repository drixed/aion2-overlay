using System.Buffers.Binary;
using AionDpsMeter.Services.PacketProcessing.Fork;
using AionDpsMeter.Services.PacketProcessing.Routing;
using AionDpsMeter.Timers.Bosses;

namespace AionDpsMeter.Timers.Zones;

/// <summary>
/// Map load, wire bytes <c>21 36</c>: <c>u32 load count, u32 map id, …</c> (layout from cyberbadger6969/aion2-dps-meter,
/// GPL-3.0; checked against an EU capture: 1110 = Altgard). Sent on entering a map and on teleports within it.
/// </summary>
public static class MapLoadParser
{
    /// <summary>RATmeter reads opcodes little-endian: wire bytes 21 36 → 0x3621.</summary>
    public const ushort Opcode = 0x3621;

    public static int? Parse(ReadOnlySpan<byte> body)
    {
        if (body.Length < 8) return null;
        var mapId = BinaryPrimitives.ReadUInt32LittleEndian(body.Slice(4, 4));
        return mapId is 0 or > 9_999_999 ? null : (int)mapId;
    }
}

/// <summary>The map the character is on, from the last map load.</summary>
public sealed class ZoneTracker
{
    private volatile int currentMapId;

    public int? CurrentMapId => currentMapId == 0 ? null : currentMapId;

    /// <summary>Raised when the character moves to another map (not on teleports within one).</summary>
    public event Action? Changed;

    public void OnMapLoaded(int mapId)
    {
        if (mapId == currentMapId) return;
        currentMapId = mapId;
        Changed?.Invoke();
    }
}

public sealed class MapLoadListener(ZoneTracker zones) : IMapLoadListener
{
    public void OnMapLoad(Packet packet)
    {
        // RATmeter frame: varint length prefix, 2 opcode bytes, body.
        if (!Wire.TryVarint(packet.Data, 0, out _, out var header) || packet.Data.Length < header + 2) return;
        if (MapLoadParser.Parse(packet.Data.AsSpan(header + 2)) is { } mapId) zones.OnMapLoaded(mapId);
    }
}
