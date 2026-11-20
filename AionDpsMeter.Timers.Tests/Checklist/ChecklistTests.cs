using AionDpsMeter.Timers.Checklist;

namespace AionDpsMeter.Timers.Tests.Checklist;

/// <summary>EU login of 2026-11-20 16:15:33: Odyle energy 115 (+415), Shugo Festival 20, Nightmare 4, Dimensional Invasion 7,
/// Ascension Trial 0, Lower Abyss 3 h 58 min left, the five daily tasks handed in, membership until 2 December.</summary>
public class ChecklistTests
{
    // one entry: mask (1 time u64, 2 one more u64, 4 count varint, 8 bonus varint) | id u32 | fields
    private static byte[] Entry(int id, int? count = null, int? bonus = null, long? timeMs = null)
    {
        var e = new List<byte> { (byte)((timeMs is null ? 0 : 1) | (count is null ? 0 : 4) | (bonus is null ? 0 : 8)) };
        e.AddRange(BitConverter.GetBytes(id));
        if (timeMs is { } t) e.AddRange(BitConverter.GetBytes(t));
        if (count is { } c) e.AddRange(Varint(c));
        if (bonus is { } b) e.AddRange(Varint(b));
        return [.. e];
    }

    private static byte[] Varint(int v)
    {
        var o = new List<byte>();
        do { var x = (byte)(v & 0x7F); v >>= 7; o.Add(v > 0 ? (byte)(x | 0x80) : x); } while (v > 0);
        return [.. o];
    }

    private static byte[] LoginList() =>
        [0x0B, 0x61, 6, .. Entry(1, 20, 3), .. Entry(3, timeMs: 14_338_000), .. Entry(4, 4, 4), .. Entry(8, 7), .. Entry(80000001, 0), .. Entry(60000001, 115, 415)];

    private static readonly DateTime LoginUtc = new(2026, 11, 20, 11, 15, 33, DateTimeKind.Utc);   // 20:15 in Seoul

    [Fact]
    public void The_login_list_gives_every_counter()
    {
        var list = TicketParser.List(LoginList())!;
        Assert.Equal(new TicketValue(20, 3, 0), list[1]);
        Assert.Equal(14_338_000, list[3].TimeMs);
        Assert.Equal(new TicketValue(115, 415, 0), list[60000001]);
        Assert.Null(TicketParser.List([0x0B, 0x61, 2, .. Entry(1, 5)]));          // cut short: not the list
    }

    [Fact]
    public void A_change_replaces_one_counter()
    {
        var change = TicketParser.Change([0x0C, 0x61, 0, .. Entry(4, 3, 4), 3])!;    // reason 3: one used
        Assert.Equal((4, new TicketValue(3, 4, 0)), change);
    }

    [Fact]
    public void The_membership_end_and_the_daily_tasks_are_read()
    {
        var entry = new byte[21];
        entry[0] = 34;
        BitConverter.GetBytes(1_796_114_167_807L).CopyTo(entry, 13);
        Assert.Equal(1_796_114_167_807L, TicketParser.MembershipEnd([0x3B, 0xE3, 1, .. entry]));
        Assert.Equal(5, TicketParser.MissionsDone([0x1C, 0x8D, 2, 0, 0, 0, 5, 0, 0, 0, 0, 0, 0, 0]));
    }

    [Fact]
    public void Counters_refill_on_the_game_clock_up_to_their_cap()
    {
        var nightmare = Tickets.Find(4)!;                                          // 2 a day at 16:00 Seoul, cap 14
        Assert.Equal(4, Tickets.Expected(nightmare, 4, LoginUtc, LoginUtc.AddHours(10), premium: true));   // 06:15 Seoul next day: not yet
        Assert.Equal(6, Tickets.Expected(nightmare, 4, LoginUtc, LoginUtc.AddHours(20), premium: true));   // past 16:00 Seoul
        Assert.Equal(14, Tickets.Expected(nightmare, 4, LoginUtc, LoginUtc.AddDays(9), premium: true));

        var shugo = Tickets.Find(1)!;                                              // cap 12, 21 with the membership
        Assert.Equal(12, Tickets.Expected(shugo, 10, LoginUtc, LoginUtc.AddDays(3), premium: false));
        Assert.Equal(19, Tickets.Expected(shugo, 10, LoginUtc, LoginUtc.AddDays(3), premium: true));
    }

    [Fact]
    public void The_checklist_keeps_the_last_login()
    {
        using var dir = new TempDir();
        var path = dir.File("checklist.json");
        var tracker = new ChecklistTracker(path);
        tracker.OnFrame(LoginUtc, LoginList());
        tracker.OnFrame(LoginUtc, [0x0C, 0x61, 0, .. Entry(4, 3, 4), 3]);
        var again = new ChecklistTracker(path).State!;
        Assert.Equal(3, again.Tickets[4].Count);
        Assert.Equal(115, again.Tickets[60000001].Count);
        Assert.Equal(LoginUtc, again.SeenAtUtc);
    }
}
