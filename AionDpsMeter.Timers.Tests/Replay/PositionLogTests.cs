using AionDpsMeter.Timers.Replay;

namespace AionDpsMeter.Timers.Tests.Replay;

public class PositionLogTests
{
    private static readonly DateTime T0 = new(2026, 11, 20, 5, 0, 0);

    private static byte[] Varint(long v)
    {
        var bytes = new List<byte>();
        do { var b = (byte)(v & 0x7f); v >>= 7; bytes.Add(v > 0 ? (byte)(b | 0x80) : b); } while (v > 0);
        return [.. bytes];
    }

    // A movement frame of another entity (1C 37): entity varint, two bytes, x f32, y f32.
    private static byte[] Move(long entity, float x, float y) =>
        [0x1c, 0x37, .. Varint(entity), 0, 0, .. BitConverter.GetBytes(x), .. BitConverter.GetBytes(y)];

    // The local player's own position (3C 37): entity varint, 03, x, y, z.
    private static byte[] Local(long entity, float x, float y) =>
        [0x3c, 0x37, .. Varint(entity), 3, .. BitConverter.GetBytes(x), .. BitConverter.GetBytes(y), .. BitConverter.GetBytes(100f)];

    [Fact]
    public void Movement_frames_read_the_entity_and_its_place()
    {
        Assert.True(PositionLog.TryRead(Move(31342, 12_500.5f, -8_000f), out var e, out var x, out var y));
        Assert.Equal((31342, 12_500.5f, -8_000f), (e, x, y));
        Assert.True(PositionLog.TryRead(Local(777, 100_000f, 2_000f), out e, out x, out y));
        Assert.Equal((777, 100_000f, 2_000f), (e, x, y));
        Assert.False(PositionLog.TryRead([0x1c, 0x37, 5, 0], out _, out _, out _));       // too short
        Assert.False(PositionLog.TryRead(Move(5, float.NaN, 1f), out _, out _, out _));    // not a place
    }

    [Fact]
    public void A_fight_keeps_only_its_players_and_boss_within_its_time()
    {
        var log = new PositionLog();
        log.Add(T0, 1, 10, 10);
        log.Add(T0.AddSeconds(5), 1, 20, 10);
        log.Add(T0.AddSeconds(5), 2, 50, 50);   // a stranger
        log.Add(T0.AddSeconds(6), 9, 0, 0);     // the boss
        log.Add(T0.AddMinutes(10), 1, 99, 99);  // after the fight

        var tracks = log.Tracks([1, 9], T0, T0.AddMinutes(2));
        Assert.Equal([1, 9], tracks.Keys.Order());
        Assert.Equal(2, tracks[1].Count);
        Assert.Equal(20f, tracks[1][1].X);
    }

    [Fact]
    public void A_saved_replay_reads_back()
    {
        using var dir = new TempDir();
        var log = new PositionLog(dir.File("Replays"));
        log.Add(T0.AddSeconds(1), 1, 10, 20);
        var id = Guid.NewGuid();
        log.SaveReplay(id, [1], T0, T0.AddMinutes(1));
        var back = log.LoadReplay(id);
        Assert.NotNull(back);
        Assert.Equal(10f, back![1][0].X);
        Assert.Null(log.LoadReplay(Guid.NewGuid()));
    }

    [Fact]
    public void The_last_place_of_an_entity_and_of_the_local_player_is_remembered()
    {
        var log = new PositionLog();
        Assert.Null(log.Last(5));
        Assert.Null(log.Me);
        log.Add(T0, 5, 10, 20);
        log.Add(T0.AddSeconds(1), 5, 30, 40);
        Assert.Equal(new Fix(T0.AddSeconds(1), 30, 40), log.Last(5));

        log.OnFrame(T0.AddSeconds(2), Local(777, 100_000f, 2_000f));
        Assert.Equal(777, log.LocalEntity);
        log.OnFrame(T0.AddSeconds(3), Move(777, 100_500f, 2_500f));
        Assert.Equal(new Fix(T0.AddSeconds(3), 100_500f, 2_500f), log.Me);
    }

    [Fact]
    public void My_place_follows_my_own_entity_even_when_another_players_correction_frame_comes()
    {
        var log = new PositionLog();
        log.OnFrame(T0, Move(777, 100_000f, 2_000f));                   // me moving
        log.OnFrame(T0.AddSeconds(1), Local(999, 50_000f, 9_000f));     // a 3C 37 about someone else
        log.OnFrame(T0.AddSeconds(2), Move(777, 100_400f, 2_000f));     // me again
        Assert.Equal(new Fix(T0.AddSeconds(2), 100_400f, 2_000f), log.MeFor(777));
        Assert.Equal(new Fix(T0.AddSeconds(1), 50_000f, 9_000f), log.MeFor(null)); // unknown who I am: the old guess
    }
}
