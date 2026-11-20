using AionDpsMeter.Timers.Diagnostics;

namespace AionDpsMeter.Timers.Tests.Diagnostics;

public sealed class PacketRecorderTests : IDisposable
{
    private static readonly DateTime T0 = new(2026, 11, 20, 7, 0, 0);
    private readonly string dir = Path.Combine(Path.GetTempPath(), "packet-recorder-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void Nothing_is_written_until_recording_starts()
    {
        var recorder = new PacketRecorder(dir);
        recorder.Record(T0, 0x5611, [1, 2, 3]);
        Assert.False(recorder.Recording);
        Assert.False(Directory.Exists(dir));
    }

    [Fact]
    public void Packets_are_written_one_per_line_until_the_time_is_up()
    {
        var recorder = new PacketRecorder(dir);
        recorder.Start(T0, TimeSpan.FromMinutes(10));
        Assert.True(recorder.Recording);
        recorder.Record(T0.AddSeconds(1), 0x5611, [0x0a, 0x11, 0x56]);
        recorder.Record(T0.AddMinutes(5), 0x2a38, [0xff]);
        recorder.Record(T0.AddMinutes(11), 0x0001, [0x00]);     // after the end: stops instead
        Assert.False(recorder.Recording);
        Assert.Equal(2, recorder.Count);

        var lines = File.ReadAllLines(recorder.File!);
        Assert.Equal(["07:00:01.000 5611 3 0A1156", "07:05:00.000 2A38 1 FF"], lines.Where(l => !l.StartsWith('#')));
        Assert.StartsWith("packets-20261120-070000", Path.GetFileName(recorder.File));
    }

    [Fact]
    public void Stop_ends_early_and_a_new_start_makes_a_new_file()
    {
        var recorder = new PacketRecorder(dir);
        recorder.Start(T0, TimeSpan.FromMinutes(10));
        recorder.Record(T0.AddSeconds(1), 0x1, [1]);
        recorder.Stop();
        recorder.Record(T0.AddSeconds(2), 0x1, [1]);
        var first = recorder.File;
        Assert.Equal(1, recorder.Count);

        recorder.Start(T0.AddMinutes(1), TimeSpan.FromMinutes(1));
        Assert.NotEqual(first, recorder.File);
        Assert.Equal(0, recorder.Count);
    }

    public void Dispose()
    {
        try { Directory.Delete(dir, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }
}
