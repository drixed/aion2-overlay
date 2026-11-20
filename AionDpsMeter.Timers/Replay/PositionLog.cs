// aion2-overlay fork: new file (see FORK_CHANGES.md).
using System.Buffers.Binary;
using System.Text.Json;
using AionDpsMeter.Services.PacketProcessing.Fork;
using AionDpsMeter.Services.PacketProcessing.Routing;
using AionDpsMeter.Timers.Bosses;

namespace AionDpsMeter.Timers.Replay;

public readonly record struct Fix(DateTime At, float X, float Y);

/// <summary>
/// "Повтор боя": the places the game sends with movements (second byte 37: 1A/1B/1C/1E/20/28/29/2A 37 put x, y two bytes
/// after the entity id, 1F/2F 37 one byte after it, 3C 37 is a player's own correction with a 03 first). Kept in memory for
/// 40 minutes; when a boss fight is saved, the tracks of its players and its boss are written to Replays/{fight}.json —
/// nobody else's. Layouts from SkeeveAN/Aion-DPS-Meter (MIT, Aion2FrameDecoder.DecodePosition/DecodeLocalPosition).
/// </summary>
public sealed class PositionLog(string? replayDir = null) : IRawPacketListener
{
    private static readonly TimeSpan Keep = TimeSpan.FromMinutes(40);
    private const int MaxFixes = 400_000;
    private const ushort LocalOpcode = 0x373C;

    private readonly Lock gate = new();
    private readonly List<(DateTime At, int Entity, float X, float Y)> fixes = [];

    public static readonly ushort[] Opcodes = [0x371A, 0x371B, 0x371C, 0x371E, 0x3720, 0x3728, 0x3729, 0x372A, 0x371F, 0x372F, LocalOpcode];

    private static int? Offset(int first) => first switch
    {
        0x1a or 0x1b or 0x1c or 0x1e or 0x20 or 0x28 or 0x29 or 0x2a => 2,
        0x1f or 0x2f => 1,
        _ => null,
    };

    /// <param name="frame">The frame from its opcode on.</param>
    public static bool TryRead(ReadOnlySpan<byte> frame, out int entity, out float x, out float y)
    {
        entity = 0; x = y = 0;
        if (frame.Length < 4 || frame[1] != 0x37) return false;
        var p = 2;
        if (!Varint(frame, ref p, out var id) || id is < 1 or > 10_000_000) return false;
        int offset;
        if (frame[0] == 0x3c)
        {
            if (p >= frame.Length || frame[p] != 3) return false;
            offset = 1;
        }
        else if (Offset(frame[0]) is { } o) offset = o;
        else return false;
        if (frame.Length < p + offset + 8) return false;
        x = BinaryPrimitives.ReadSingleLittleEndian(frame[(p + offset)..]);
        y = BinaryPrimitives.ReadSingleLittleEndian(frame[(p + offset + 4)..]);
        if (!float.IsFinite(x) || !float.IsFinite(y) || Math.Abs(x) is <= 1 or >= 500_000 || Math.Abs(y) is <= 1 or >= 500_000) return false;
        entity = (int)id;
        return true;
    }

    public void Add(DateTime at, int entity, float x, float y)
    {
        lock (gate)
        {
            fixes.Add((at, entity, x, y));
            if (fixes.Count > MaxFixes || (fixes.Count % 5000 == 0 && at - fixes[0].At > Keep))
            {
                // too old, and when still too many the oldest quarter (the cut is taken before anything is removed)
                var cut = fixes.Count > MaxFixes ? fixes[fixes.Count / 4].At : DateTime.MinValue;
                fixes.RemoveAll(f => at - f.At > Keep || f.At < cut);
            }
        }
    }

    public void OnRawPacket(ushort opcode, Packet packet)
    {
        if ((opcode >> 8) != 0x37) return;
        try
        {
            if (!Wire.TryVarint(packet.Data, 0, out _, out var header) || packet.Data.Length < header + 4) return;
            if (TryRead(packet.Data.AsSpan(header), out var entity, out var x, out var y)) Add(DateTime.Now, entity, x, y);
        }
        catch (Exception) { }
    }

    public Dictionary<int, List<Fix>> Tracks(IEnumerable<int> entities, DateTime from, DateTime to)
    {
        var wanted = entities.ToHashSet();
        lock (gate)
        {
            return fixes.Where(f => wanted.Contains(f.Entity) && f.At >= from && f.At <= to)
                .GroupBy(f => f.Entity)
                .ToDictionary(g => g.Key, g => g.OrderBy(f => f.At).Select(f => new Fix(f.At, f.X, f.Y)).ToList());
        }
    }

    /// <summary>Writes the tracks of a fight's players and boss (a few seconds around it); nothing when none was seen.</summary>
    public void SaveReplay(Guid fight, IEnumerable<int> entities, DateTime from, DateTime to)
    {
        if (replayDir is null) return;
        var tracks = Tracks(entities, from.AddSeconds(-3), to.AddSeconds(3));
        if (tracks.Count == 0) return;
        try
        {
            Directory.CreateDirectory(replayDir);
            File.WriteAllText(Path.Combine(replayDir, $"{fight:N}.json"), JsonSerializer.Serialize(tracks.ToDictionary(kv => kv.Key.ToString(), kv => kv.Value)));
        }
        catch (IOException) { }
    }

    public Dictionary<int, List<Fix>>? LoadReplay(Guid fight)
    {
        if (replayDir is null) return null;
        try
        {
            var file = Path.Combine(replayDir, $"{fight:N}.json");
            if (!File.Exists(file)) return null;
            var raw = JsonSerializer.Deserialize<Dictionary<string, List<Fix>>>(File.ReadAllText(file));
            return raw?.Where(kv => int.TryParse(kv.Key, out _)).ToDictionary(kv => int.Parse(kv.Key), kv => kv.Value);
        }
        catch (Exception) { return null; }
    }

    private static bool Varint(ReadOnlySpan<byte> data, ref int p, out long value)
    {
        value = 0;
        for (var i = 0; i < 5 && p < data.Length; i++)
        {
            var b = data[p++];
            value |= (long)(b & 0x7f) << (7 * i);
            if ((b & 0x80) == 0) return true;
        }
        return false;
    }
}
