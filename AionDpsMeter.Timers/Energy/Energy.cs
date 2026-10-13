using System.Text.Json;
using AionDpsMeter.Timers.Bosses;

namespace AionDpsMeter.Timers.Energy;

/// <summary>In game: "75 (+990)/840" — <paramref name="Current"/> 75, <paramref name="Extra"/> 990 (the maximum is not in
/// the packet; it comes from schedule.json).</summary>
public sealed record EnergyReading(int Current, int Extra);

/// <summary>A partial change: one of the pools (the other is unchanged). When only the extra went down, the current is
/// already at 0: the game spends the current energy first (seen: "10 (+990)" → "0 (+920)" for an entry costing 80).</summary>
public sealed record EnergyUpdate(int? Current, int? Extra)
{
    public EnergyReading ApplyTo(EnergyReading reading) => (Current, Extra) switch
    {
        ({ } c, { } x) => new EnergyReading(c, x),
        ({ } c, null) => reading with { Current = c },
        (null, { } x) when x < reading.Extra => new EnergyReading(0, x),
        (null, { } x) => reading with { Extra = x },
        _ => reading,
    };
}

/// <param name="FromEarlierSession">Loaded from disk: the game has not sent energy since the app started.</param>
public sealed record EnergyState(EnergyReading Reading, DateTimeOffset At, bool FromEarlierSession);

/// <summary>
/// Energy, wire bytes <c>0C 61</c> (EU captures since 2026-10-07): <c>kind (01 full / 00 change), field mask, 01, id varint
/// (51591), [current varint if mask has 04], [extra varint if mask has 08], …</c>. Seen: <c>01 0C</c> every ~5 minutes as
/// energy refills, <c>00 08</c> when an entry was paid from the extra pool, <c>00 0C</c> when it was paid from the current
/// one (2026-10-13). Other records of the opcode have no 01 and energy id after the mask.
/// </summary>
public static class EnergyParser
{
    /// <summary>RATmeter reads opcodes little-endian: wire bytes 0C 61 → 0x610C.</summary>
    public const ushort Opcode = 0x610C;

    /// <summary>The energy's id inside the 0C 61 records (the same in every EU capture so far).</summary>
    private const long EnergyId = 51591;

    private const byte CurrentField = 0x04, ExtraField = 0x08;

    /// <summary>Both pools: a full record, or a change of both.</summary>
    public static EnergyReading? Parse(ReadOnlySpan<byte> body) =>
        Read(body) is { Current: { } c, Extra: { } x } ? new EnergyReading(c, x) : null;

    /// <summary>One pool only.</summary>
    public static EnergyUpdate? ParseUpdate(ReadOnlySpan<byte> body) =>
        Read(body) is { } u && (u.Current is null || u.Extra is null) ? u : null;

    private static EnergyUpdate? Read(ReadOnlySpan<byte> body)
    {
        if (body.Length < 5 || body[0] > 0x01 || body[2] != 0x01) return null;
        var mask = body[1];
        if ((mask & (CurrentField | ExtraField)) == 0 || (mask & ~(CurrentField | ExtraField)) != 0) return null;
        var o = 3;
        if (!Wire.TryVarint(body, o, out var id, out var len) || id != EnergyId) return null;
        o += len;
        int? current = null, extra = null;
        if ((mask & CurrentField) != 0)
        {
            if (!Wire.TryVarint(body, o, out var c, out len) || c is < 0 or > 100_000) return null;
            current = (int)c;
            o += len;
        }
        if ((mask & ExtraField) != 0)
        {
            if (!Wire.TryVarint(body, o, out var x, out _) || x is < 0 or > 1_000_000) return null;
            extra = (int)x;
        }
        return new EnergyUpdate(current, extra);
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

    /// <summary>A partial change applies to the last known reading; with none known yet it is ignored.</summary>
    public void OnUpdate(EnergyUpdate update)
    {
        if (current is { } known) OnReading(update.ApplyTo(known.Reading));
    }

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
        var body = packet.Data.AsSpan(header + 2);
        if (EnergyParser.Parse(body) is { } reading) tracker.OnReading(reading);
        else if (EnergyParser.ParseUpdate(body) is { } update) tracker.OnUpdate(update);
    }
}
