using System.Text.Json;
using AionDpsMeter.Timers.Bosses;

namespace AionDpsMeter.Timers.Energy;

/// <summary>In game: "75 (+990)/840" — <paramref name="Current"/> 75, <paramref name="Extra"/> 990 (the maximum is not in
/// the packet; it comes from schedule.json).</summary>
public sealed record EnergyReading(int Current, int Extra);

/// <param name="FromEarlierSession">Loaded from disk: the game has not sent energy since the app started.</param>
public sealed record EnergyState(EnergyReading Reading, DateTimeOffset At, bool FromEarlierSession);

/// <summary>
/// Energy update, wire bytes <c>0C 61</c> (found in EU captures 2026-10-07, sent about every 5 minutes as energy refills):
/// <c>01 0C 01, id varint (51591), current varint, extra varint, 01, step varint, u32 0</c>.
/// The same opcode carries other records (first byte 00); only this shape is energy.
/// </summary>
public static class EnergyParser
{
    /// <summary>RATmeter reads opcodes little-endian: wire bytes 0C 61 → 0x610C.</summary>
    public const ushort Opcode = 0x610C;

    public static EnergyReading? Parse(ReadOnlySpan<byte> body)
    {
        if (body.Length < 6 || body[0] != 0x01 || body[1] != 0x0C || body[2] != 0x01) return null;
        var o = 3;
        if (!Wire.TryVarint(body, o, out _, out var len)) return null;
        o += len;
        if (!Wire.TryVarint(body, o, out var current, out len) || current is < 0 or > 100_000) return null;
        o += len;
        if (!Wire.TryVarint(body, o, out var extra, out len) || extra is < 0 or > 1_000_000) return null;
        return new EnergyReading((int)current, (int)extra);
    }
}

/// <summary>The last energy the game sent, kept on disk so the overlay has a number right after start.</summary>
public sealed class EnergyTracker
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    private readonly TimeProvider time;
    private readonly string path;
    private volatile EnergyState? current;

    public EnergyTracker(TimeProvider time, string path)
    {
        this.time = time;
        this.path = path;
        try
        {
            if (File.Exists(path) && JsonSerializer.Deserialize<EnergyState>(File.ReadAllText(path), Json) is { Reading: not null } saved)
                current = saved with { FromEarlierSession = true };
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException) { }
    }

    public EnergyState? Current => current;

    public event Action? Changed;

    public void OnReading(EnergyReading reading)
    {
        var state = new EnergyState(reading, time.GetUtcNow(), FromEarlierSession: false);
        current = state;
        try
        {
            File.WriteAllText(path, JsonSerializer.Serialize(state, Json));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        Changed?.Invoke();
    }
}

public sealed class EnergyListener(EnergyTracker tracker) : AionDpsMeter.Services.PacketProcessing.Fork.IEnergyListener
{
    public void OnEnergyPacket(AionDpsMeter.Services.PacketProcessing.Routing.Packet packet)
    {
        // RATmeter frame: varint length prefix, 2 opcode bytes, body.
        if (!Wire.TryVarint(packet.Data, 0, out _, out var header) || packet.Data.Length < header + 2) return;
        if (EnergyParser.Parse(packet.Data.AsSpan(header + 2)) is { } reading) tracker.OnReading(reading);
    }
}
