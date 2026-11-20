// aion2-overlay fork: new file (see FORK_CHANGES.md).
using System.Buffers.Binary;
using System.Text.Json;
using AionDpsMeter.Services.PacketProcessing.Fork;
using AionDpsMeter.Services.PacketProcessing.Routing;

namespace AionDpsMeter.Timers.Bosses;

/// <summary>A skill being wound up: who casts it, at whom (the caster itself = around it), and how long the cast takes.</summary>
public sealed record BossCast(int Caster, int Skill, int Target, DateTime Start, TimeSpan Duration)
{
    public DateTime End => Start + Duration;

    /// <summary>Aimed at the caster itself (or at no one): an area around the boss, not one player.</summary>
    public bool AroundCaster => Target == Caster || Target == 0;
}

/// <summary>How hard the skill hit me before, against my HP: lethal (it took ~all of it), dangerous (a third or more), or not known.</summary>
public enum CastDanger { None, Dangerous, Lethal }

/// <summary>
/// The windup frame (0x3809; Krao Cave, EU 2026-11-20): <c>09 38 | caster varint | 1 byte | skill u32 | 1 byte | aim flags |
/// target varint | facing f32 | (aim flags &amp; 2: x, y, z f32) | cast time ms varint | …</c>. The hit of the same skill follows
/// when the cast ends. Layout from SkeeveAN/Aion-DPS-Meter (MIT, Aion2FrameDecoder.DecodeWindup / TryReadAim), written anew here.
/// </summary>
public static class CastParser
{
    public const ushort WindupOpcode = 0x3809;

    /// <param name="frame">The frame from its opcode on.</param>
    public static BossCast? Windup(DateTime at, ReadOnlySpan<byte> frame)
    {
        if (frame.Length < 12 || frame[0] != 0x09 || frame[1] != 0x38) return null;
        var p = 2;
        if (!Varint(frame, ref p, out var caster) || caster is < 1 or > 10_000_000 || p + 8 > frame.Length) return null;
        p += 1;
        var skill = BinaryPrimitives.ReadUInt32LittleEndian(frame[p..]);
        p += 4;
        if (skill is < 1 or > 300_000_000) return null;
        var aim = frame[p + 1];
        p += 2;
        if (!Varint(frame, ref p, out var target) || target is < 0 or > 10_000_000 || p + 4 > frame.Length) return null;
        var facing = BinaryPrimitives.ReadSingleLittleEndian(frame[p..]);
        p += 4;
        if (!float.IsFinite(facing) || Math.Abs(facing) > 720) return null;
        if ((aim & 2) != 0)
        {
            if (p + 12 > frame.Length) return null;
            p += 12;
        }
        if (!Varint(frame, ref p, out var ms) || ms is < 50 or > 120_000) return null;
        return new BossCast((int)caster, (int)skill, (int)target, at, TimeSpan.FromMilliseconds(ms));
    }

    private static bool Varint(ReadOnlySpan<byte> b, ref int p, out long value)
    {
        if (!Wire.TryVarint(b, p, out value, out var length)) return false;
        p += length;
        return true;
    }
}

/// <summary>
/// The casts of the bosses being fought (the casters <paramref name="watched"/> takes), for the boss cast overlay, and the hardest
/// hit each boss skill ever dealt me (boss-hits.json), which marks a cast lethal or dangerous against my HP.
/// </summary>
public sealed class BossCastTracker(Func<int, bool> watched, string? path = null) : IRawPacketListener
{
    /// <summary>A cast stays on screen this long after it went off, so its end is seen.</summary>
    public static readonly TimeSpan Linger = TimeSpan.FromSeconds(0.5);

    private readonly Lock gate = new();
    private readonly List<BossCast> casts = [];
    private Dictionary<int, long>? maxHits;

    public event Action? Changed;

    public void OnRawPacket(ushort opcode, Packet packet)
    {
        if (opcode != CastParser.WindupOpcode) return;
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
        if (CastParser.Windup(at, frame) is not { } cast || !watched(cast.Caster)) return;
        lock (gate)
        {
            casts.RemoveAll(c => c.End + Linger < at || (c.Caster == cast.Caster && c.Skill == cast.Skill));
            casts.Add(cast);
        }
        Changed?.Invoke();
    }

    /// <summary>The casts going on (and those that just went off), the one ending first on top.</summary>
    public IReadOnlyList<BossCast> Active(DateTime now)
    {
        lock (gate) return casts.Where(c => c.Start <= now && now <= c.End + Linger).OrderBy(c => c.End).ToList();
    }

    /// <summary>A boss skill hit me: the hardest hit of each skill is kept.</summary>
    public void OnHitOnMe(int skill, long damage)
    {
        if (damage <= 0) return;
        lock (gate)
        {
            var hits = Hits();
            if (hits.GetValueOrDefault(skill) >= damage) return;
            hits[skill] = damage;
            if (path is null) return;
            try { File.WriteAllText(path, JsonSerializer.Serialize(hits)); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }

    public long? MaxHit(int skill)
    {
        lock (gate) return Hits().TryGetValue(skill, out var v) ? v : null;
    }

    /// <summary>Against my maximum HP: lethal when the skill once took 90 % or more of it, dangerous from a third.</summary>
    public CastDanger Danger(int skill, long myMaxHp)
    {
        if (myMaxHp <= 0 || MaxHit(skill) is not { } hit) return CastDanger.None;
        if (hit >= myMaxHp * 0.9) return CastDanger.Lethal;
        return hit >= myMaxHp * 0.35 ? CastDanger.Dangerous : CastDanger.None;
    }

    private Dictionary<int, long> Hits()
    {
        if (maxHits is not null) return maxHits;
        try { maxHits = path is not null && File.Exists(path) ? JsonSerializer.Deserialize<Dictionary<int, long>>(File.ReadAllText(path)) : null; }
        catch (Exception) { maxHits = null; }
        return maxHits ??= [];
    }
}
