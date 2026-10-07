using AionDpsMeter.Services.PacketProcessing.Routing;
using AionDpsMeter.Timers.Bosses;
using AionDpsMeter.Timers.Schedule;
using AionDpsMeter.Timers.Tests.Runtime;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace AionDpsMeter.Timers.Tests.Bosses;

public class FieldBossListListenerTests
{
    /// <summary>RATmeter's frame: varint length prefix, opcode bytes 01 91, body.</summary>
    private static Packet Frame(byte[] body)
    {
        var payload = new List<byte> { 0x01, 0x91 };
        payload.AddRange(body);
        var head = new List<byte>();
        var v = (uint)(payload.Count + 2);
        do
        {
            var b = (byte)(v & 0x7F);
            v >>= 7;
            if (v != 0) b |= 0x80;
            head.Add(b);
        } while (v != 0);
        return new Packet { Data = [.. head, .. payload], ReceivedAt = 0 };
    }

    private static FieldBossTracker Tracker() =>
        new(BossCatalog.Empty, () => ScheduleData.Empty, new FakeTimeProvider(DateTimeOffset.UtcNow));

    [Fact]
    public void A_list_packet_reaches_the_tracker_under_the_current_server()
    {
        var tracker = Tracker();
        var listener = new FieldBossListListener(tracker, new FakeServer(5), NullLogger<FieldBossListListener>.Instance);

        listener.OnFieldBossList(Frame(Fixtures.Bytes(Fixtures.AltgardList)));

        Assert.Equal(24, tracker.TimersFor(5).Count);
    }

    [Fact]
    public void A_garbage_packet_is_dropped_quietly()
    {
        var tracker = Tracker();
        var listener = new FieldBossListListener(tracker, new FakeServer(5), NullLogger<FieldBossListListener>.Instance);

        listener.OnFieldBossList(new Packet { Data = [0xFF, 0xFF], ReceivedAt = 0 });
        listener.OnFieldBossList(Frame([1, 2, 3]));

        Assert.Empty(tracker.TimersFor(5));
    }
}
