using AionDpsMeter.Services.PacketCapture.Fork;

namespace AionDpsMeter.Timers.Tests.Capture;

/// <summary>
/// Segments of the game's TCP stream can arrive late, twice or out of order. Upstream jumped over a gap and dropped
/// the late segment as "old" (841 small gaps in the user's logs, mostly one 1412-byte segment), losing hits.
/// </summary>
public class TcpSegmentOrdererTests
{
    private const string S = "Client:50000";

    private static byte[] Bytes(int from, int count) => Enumerable.Range(from, count).Select(i => (byte)i).ToArray();

    private static byte[] Joined(IEnumerable<byte[]> chunks) => chunks.SelectMany(c => c).ToArray();

    [Fact]
    public void In_order_segments_pass_straight_through()
    {
        var o = new TcpSegmentOrderer();
        Assert.Equal(Bytes(0, 10), Joined(o.Accept(S, 1000, Bytes(0, 10), 0)));
        Assert.Equal(Bytes(10, 10), Joined(o.Accept(S, 1010, Bytes(10, 10), 1)));
    }

    [Fact]
    public void A_late_segment_is_put_back_in_its_place()
    {
        var o = new TcpSegmentOrderer();
        o.Accept(S, 1000, Bytes(0, 10), 0);
        Assert.Empty(Joined(o.Accept(S, 1020, Bytes(20, 10), 1)));           // the next one is missing: wait
        Assert.Equal(Bytes(10, 20), Joined(o.Accept(S, 1010, Bytes(10, 10), 2))); // it arrives: both, in order
    }

    [Fact]
    public void A_duplicate_is_dropped_and_an_overlapping_resend_trimmed()
    {
        var o = new TcpSegmentOrderer();
        o.Accept(S, 1000, Bytes(0, 10), 0);
        Assert.Empty(Joined(o.Accept(S, 1000, Bytes(0, 10), 1)));
        Assert.Equal(Bytes(10, 5), Joined(o.Accept(S, 1005, Bytes(5, 10), 2)));
    }

    [Fact]
    public void A_gap_that_never_fills_is_skipped_after_a_while()
    {
        var o = new TcpSegmentOrderer();
        o.Accept(S, 1000, Bytes(0, 10), 0);
        Assert.Empty(Joined(o.Accept(S, 1020, Bytes(20, 10), 10)));
        Assert.Equal(Bytes(20, 20), Joined(o.Accept(S, 1030, Bytes(30, 10), 10 + TcpSegmentOrderer.GapWaitMs + 1)));
    }

    [Fact]
    public void A_huge_jump_is_a_new_connection_not_a_gap()
    {
        var o = new TcpSegmentOrderer();
        o.Accept(S, 1000, Bytes(0, 10), 0);
        Assert.Equal(Bytes(50, 10), Joined(o.Accept(S, 100_000_000, Bytes(50, 10), 1)));
        Assert.Equal(Bytes(60, 10), Joined(o.Accept(S, 100_000_010, Bytes(60, 10), 2)));
    }

    [Fact]
    public void Sequence_numbers_wrap_around()
    {
        var o = new TcpSegmentOrderer();
        o.Accept(S, uint.MaxValue - 4, Bytes(0, 10), 0);        // ends past 2^32
        Assert.Equal(Bytes(10, 10), Joined(o.Accept(S, 5, Bytes(10, 10), 1)));
    }

    [Fact]
    public void Streams_are_ordered_separately()
    {
        var o = new TcpSegmentOrderer();
        o.Accept("A", 1000, Bytes(0, 10), 0);
        Assert.Equal(Bytes(0, 10), Joined(o.Accept("B", 5000, Bytes(0, 10), 0)));
    }
}
