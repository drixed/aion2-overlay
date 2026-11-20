// aion2-overlay fork: new file (see FORK_CHANGES.md).
using System.Buffers.Binary;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using AionDpsMeter.Services.PacketProcessing.Routing;
using AionDpsMeter.Timers.Bosses;

namespace AionDpsMeter.Timers.Checklist;

/// <summary>What the game reports for one counter ("contents ticket"): what is left, the bonus (Odyle energy: the charged part), time left in ms.</summary>
public readonly record struct TicketValue(int Count, int Bonus, long TimeMs);

/// <summary>One counter of the game: its Russian name, cap (with the membership), whether it is a time, and when it refills (game clock, Seoul).</summary>
public sealed record TicketDef(int Id, string Name, int Max, int SubscribeMax, bool IsTime, TimeSpan? DailyAt, int DailyCount, TimeSpan? WeeklyAt, int WeeklyCount, DayOfWeek WeeklyDay);

/// <summary>The counters (Data/tickets.ru.json, from SkeeveAN/Aion-DPS-Meter, MIT — see NOTICE.md) and how they refill.</summary>
public static class Tickets
{
    public const int Odyle = 60000001;
    public const int DailyDungeon = 101;
    public const int LowerAbyss = 3;

    /// <summary>The game's resets ("16:00", Wednesdays for the weekly ones) are Korean time.</summary>
    public static readonly TimeSpan GameClock = TimeSpan.FromHours(9);

    private static readonly Lazy<Dictionary<int, TicketDef>> Catalog = new(Load);

    public static TicketDef? Find(int id) => Catalog.Value.GetValueOrDefault(id);

    /// <summary>The count now, worked out from the one read at <paramref name="seenAtUtc"/>: daily refills up to the cap, a weekly refill sets the count.</summary>
    public static int Expected(TicketDef def, int count, DateTime seenAtUtc, DateTime nowUtc, bool premium)
    {
        var seen = seenAtUtc + GameClock;
        var now = nowUtc + GameClock;
        var cap = premium ? Math.Max(def.Max, def.SubscribeMax) : def.Max;
        var value = count;
        if (def.WeeklyAt is { } weekly && def.WeeklyCount > 0)
        {
            var w = seen.Date + weekly;
            while (w <= seen || w.DayOfWeek != def.WeeklyDay) w = w.AddDays(1);
            if (w <= now) value = def.WeeklyCount;
        }
        if (def.DailyAt is { } daily && def.DailyCount > 0)
        {
            var d = seen.Date + daily;
            if (d <= seen) d = d.AddDays(1);
            for (; d <= now && value < cap; d = d.AddDays(1)) value = Math.Min(cap, value + def.DailyCount);
        }
        return value;
    }

    private static Dictionary<int, TicketDef> Load()
    {
        var map = new Dictionary<int, TicketDef>();
        try
        {
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("tickets.ru.json");
            if (stream is null) return map;
            using var doc = JsonDocument.Parse(stream);
            foreach (var t in doc.RootElement.GetProperty("tickets").EnumerateArray())
            {
                TimeSpan? dailyAt = null, weeklyAt = null;
                int dailyCount = 0, weeklyCount = 0;
                var weeklyDay = DayOfWeek.Wednesday;
                if (t.TryGetProperty("daily", out var d))
                {
                    dailyAt = TimeSpan.Parse(d[0].GetString()!, CultureInfo.InvariantCulture);
                    dailyCount = d[1].GetInt32();
                }
                if (t.TryGetProperty("weekly", out var w))
                {
                    weeklyAt = TimeSpan.Parse(w[0].GetString()!, CultureInfo.InvariantCulture);
                    weeklyCount = w[1].GetInt32();
                    weeklyDay = Enum.Parse<DayOfWeek>(w[2].GetString()!);
                }
                var id = t.GetProperty("id").GetInt32();
                map[id] = new TicketDef(id, t.GetProperty("n").GetString() ?? $"#{id}", t.GetProperty("max").GetInt32(), t.GetProperty("sub").GetInt32(),
                    t.TryGetProperty("time", out _), dailyAt, dailyCount, weeklyAt, weeklyCount, weeklyDay);
            }
        }
        catch (Exception ex) when (ex is JsonException or FormatException or KeyNotFoundException or InvalidOperationException or ArgumentException) { }
        return map;
    }
}

/// <summary>
/// The account status frames (EU login 2026-11-20; layouts from SkeeveAN/Aion-DPS-Meter, MIT, Aion2Tickets, written anew here):
/// <list type="bullet">
/// <item><c>0b 61 | n | n × entry</c> at login, <c>0c 61 | flag | entry | reason | (flag 1: gained u32)</c> for a change; an entry is
/// <c>mask | id u32 | fields</c> — mask 1 time left u64 ms, 2 one more u64 (skipped), 4 count varint, 8 bonus varint;</item>
/// <item><c>3b e3 | n | n × 21 bytes</c>: the membership (benefits 34–42, end as ms since 1970 at +13);</item>
/// <item><c>1c 8d | taken u32 | handed in u32 | …</c>: the head of the quest list, the five daily tasks.</item>
/// </list>
/// </summary>
public static class TicketParser
{
    public const ushort ListOpcode = 0x610B, ChangeOpcode = 0x610C, MembershipOpcode = 0xE33B, MissionsOpcode = 0x8D1C;
    public const int MissionsPerDay = 5;

    /// <param name="frame">From the opcode on; the list must read to its last byte.</param>
    public static Dictionary<int, TicketValue>? List(ReadOnlySpan<byte> frame)
    {
        if (frame.Length < 3 || frame[0] != 0x0B || frame[1] != 0x61) return null;
        var list = new Dictionary<int, TicketValue>();
        var p = 3;
        for (var i = 0; i < frame[2]; i++)
        {
            if (!Entry(frame, ref p, out var id, out var value)) return null;
            list[id] = value;
        }
        return p == frame.Length ? list : null;
    }

    public static (int Id, TicketValue Value)? Change(ReadOnlySpan<byte> frame)
    {
        if (frame.Length < 4 || frame[0] != 0x0C || frame[1] != 0x61) return null;
        var p = 3;
        return Entry(frame, ref p, out var id, out var value) && p < frame.Length ? (id, value) : null;
    }

    /// <summary>The end of the membership in ms since 1970, 0 for none; null when the frame is not the membership list.</summary>
    public static long? MembershipEnd(ReadOnlySpan<byte> frame)
    {
        if (frame.Length < 3 || frame[0] != 0x3B || frame[1] != 0xE3 || frame.Length != 3 + 21 * frame[2]) return null;
        long end = 0;
        for (var i = 0; i < frame[2]; i++)
        {
            var entry = frame.Slice(3 + 21 * i, 21);
            var stop = BinaryPrimitives.ReadUInt64LittleEndian(entry[13..]);
            if (entry[0] is >= 34 and <= 42 && stop is > 1_500_000_000_000 and < 4_000_000_000_000) end = Math.Max(end, (long)stop);
        }
        return end;
    }

    /// <summary>The daily tasks handed in (0–5), null when the frame is no quest list head.</summary>
    public static int? MissionsDone(ReadOnlySpan<byte> frame)
    {
        if (frame.Length < 14 || frame[0] != 0x1C || frame[1] != 0x8D) return null;
        var taken = BinaryPrimitives.ReadUInt32LittleEndian(frame[2..]);
        var done = BinaryPrimitives.ReadUInt32LittleEndian(frame[6..]);
        if (taken > 20 || done > 20) return null;
        return (int)Math.Min(done, MissionsPerDay);
    }

    private static bool Entry(ReadOnlySpan<byte> frame, ref int p, out int id, out TicketValue value)
    {
        id = 0;
        value = default;
        if (p + 5 > frame.Length) return false;
        int mask = frame[p++];
        if ((mask & ~0x0F) != 0) return false;
        id = (int)BinaryPrimitives.ReadUInt32LittleEndian(frame[p..]);
        p += 4;
        long time = 0, count = 0, bonus = 0;
        if ((mask & 1) != 0)
        {
            if (p + 8 > frame.Length) return false;
            time = (long)BinaryPrimitives.ReadUInt64LittleEndian(frame[p..]);
            p += 8;
        }
        if ((mask & 2) != 0)
        {
            if (p + 8 > frame.Length) return false;
            p += 8;
        }
        if ((mask & 4) != 0 && !Varint(frame, ref p, out count)) return false;
        if ((mask & 8) != 0 && !Varint(frame, ref p, out bonus)) return false;
        value = new TicketValue((int)count, (int)bonus, time);
        return true;
    }

    private static bool Varint(ReadOnlySpan<byte> b, ref int p, out long value)
    {
        if (!Wire.TryVarint(b, p, out value, out var length)) return false;
        p += length;
        return true;
    }
}

/// <summary>The checklist as of the last login: counters, membership end, daily tasks (and when that was read).</summary>
public sealed record ChecklistState(DateTime SeenAtUtc, Dictionary<int, TicketValue> Tickets, long MembershipEndMs, int? Missions, DateTime? MissionsAtUtc);

/// <summary>Keeps the last login's counters in checklist.json and follows the changes the game sends.</summary>
public sealed class ChecklistTracker(string path)
{
    private readonly Lock gate = new();
    private ChecklistState? state;
    private bool loaded;

    public event Action? Changed;

    public ChecklistState? State
    {
        get
        {
            lock (gate)
            {
                if (!loaded) { loaded = true; state ??= Load(); }
                return state;
            }
        }
    }

    public void OnRawPacket(ushort opcode, Packet packet)
    {
        if (opcode is not (TicketParser.ListOpcode or TicketParser.ChangeOpcode or TicketParser.MembershipOpcode or TicketParser.MissionsOpcode)) return;
        try
        {
            if (!Wire.TryVarint(packet.Data, 0, out _, out var header) || packet.Data.Length < header + 3) return;
            OnFrame(DateTime.UtcNow, packet.Data.AsSpan(header));
        }
        catch (Exception) { }
    }

    /// <param name="frame">The frame from its opcode on.</param>
    public void OnFrame(DateTime nowUtc, ReadOnlySpan<byte> frame)
    {
        if (frame.Length < 2) return;
        var opcode = (ushort)(frame[0] | frame[1] << 8);
        lock (gate)
        {
            if (!loaded) { loaded = true; state ??= Load(); }
            switch (opcode)
            {
                case TicketParser.ListOpcode when TicketParser.List(frame) is { } list:
                    state = new ChecklistState(nowUtc, list, state?.MembershipEndMs ?? 0, state?.Missions, state?.MissionsAtUtc);
                    break;
                case TicketParser.ChangeOpcode when state is not null && TicketParser.Change(frame) is { } change:
                    var tickets = new Dictionary<int, TicketValue>(state.Tickets) { [change.Id] = change.Value };
                    state = state with { Tickets = tickets };
                    break;
                case TicketParser.MembershipOpcode when TicketParser.MembershipEnd(frame) is { } end && state is not null:
                    state = state with { MembershipEndMs = end };
                    break;
                case TicketParser.MissionsOpcode when TicketParser.MissionsDone(frame) is { } done && state is not null:
                    state = state with { Missions = done, MissionsAtUtc = nowUtc };
                    break;
                default:
                    return;
            }
            try { File.WriteAllText(path, JsonSerializer.Serialize(state)); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
        Changed?.Invoke();
    }

    private ChecklistState? Load()
    {
        try { return File.Exists(path) ? JsonSerializer.Deserialize<ChecklistState>(File.ReadAllText(path)) : null; }
        catch (Exception) { return null; }
    }
}
