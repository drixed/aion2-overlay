using AionDpsMeter.Timers.Energy;
using AionDpsMeter.Services.PacketProcessing.Routing;
using Microsoft.Extensions.Time.Testing;

namespace AionDpsMeter.Timers.Tests.Energy;

public class EnergyTests
{
    // Live EU packets (2026-10-07), body after the opcode 0C 61; in game: "75 (+950)/840", then "75 (+990)/840".
    private const string At2038 = "010C018793034BB6070128000000";
    private const string At2044 = "010C018793034BDE070128000000";

    [Fact]
    public void The_energy_update_reads_current_and_extra()
    {
        Assert.Equal(new EnergyReading(75, 950), EnergyParser.Parse(Convert.FromHexString(At2038)));
        Assert.Equal(new EnergyReading(75, 990), EnergyParser.Parse(Convert.FromHexString(At2044)));
    }

    [Fact]
    public void An_extra_only_update_changes_the_extra_and_keeps_the_current()
    {
        // 21:46:45, after entering a dungeon: "00 08 01, id 51591, extra 920" — the extra pool paid the entry.
        var update = EnergyParser.ParseUpdate(Convert.FromHexString("000801879303980703"));
        Assert.Equal(new EnergyUpdate(Extra: 920), update);
        // In game: "10 (+990)" → "0 (+920)": the entry cost 80, the current is spent first, the rest from the extra.
        Assert.Equal(new EnergyReading(0, 920), update!.ApplyTo(new EnergyReading(10, 990)));
        Assert.Equal(new EnergyReading(10, 1030), new EnergyUpdate(1030).ApplyTo(new EnergyReading(10, 990))); // a refill keeps the current
        Assert.Null(EnergyParser.ParseUpdate(Convert.FromHexString("000801FFFF03980703"))); // another resource id
    }

    [Fact]
    public void Other_shapes_of_the_same_opcode_are_ignored()
    {
        Assert.Null(EnergyParser.Parse(Convert.FromHexString("00040C0000000E02")));
        Assert.Null(EnergyParser.Parse(Convert.FromHexString("000106000000C01A11000000000003")));
        Assert.Null(EnergyParser.Parse([1, 0x0C]));
        Assert.Null(EnergyParser.Parse([]));
    }

    [Fact]
    public void The_tracker_keeps_the_last_reading_and_survives_a_restart()
    {
        using var dir = new TempDir();
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 10, 7, 15, 38, 50, TimeSpan.Zero));
        var tracker = new EnergyTracker(time, dir.File("energy.json"));
        Assert.Null(tracker.Current);
        var changes = 0;
        tracker.Changed += () => changes++;

        tracker.OnReading(new EnergyReading(75, 950));
        Assert.Equal(new EnergyReading(75, 950), tracker.Current!.Reading);
        Assert.False(tracker.Current.FromEarlierSession);
        Assert.Equal(1, changes);

        var restarted = new EnergyTracker(time, dir.File("energy.json"));
        Assert.Equal(new EnergyReading(75, 950), restarted.Current!.Reading);
        Assert.True(restarted.Current.FromEarlierSession); // shown as "last known" until the game sends it again
        Assert.Equal(time.GetUtcNow(), restarted.Current.At);
    }

    [Fact]
    public void The_listener_feeds_framed_packets_to_the_tracker()
    {
        using var dir = new TempDir();
        var tracker = new EnergyTracker(new FakeTimeProvider(), dir.File("energy.json"));
        var listener = new EnergyListener(tracker);
        var body = Convert.FromHexString(At2044);
        listener.OnEnergyPacket(new Packet { Data = [(byte)(body.Length + 4), 0x0C, 0x61, .. body], ReceivedAt = 0 });
        listener.OnEnergyPacket(new Packet { Data = [0x05, 0x0C, 0x61, 0x00, 0x04], ReceivedAt = 0 });
        Assert.Equal(new EnergyReading(75, 990), tracker.Current!.Reading);
        var spend = Convert.FromHexString("000801879303980703");
        listener.OnEnergyPacket(new Packet { Data = [(byte)(spend.Length + 4), 0x0C, 0x61, .. spend], ReceivedAt = 0 });
        Assert.Equal(new EnergyReading(0, 920), tracker.Current!.Reading);
    }
}
