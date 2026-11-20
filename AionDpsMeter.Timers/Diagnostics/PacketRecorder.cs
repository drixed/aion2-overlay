// aion2-overlay fork: new file (see FORK_CHANGES.md).
namespace AionDpsMeter.Timers.Diagnostics;

/// <summary>
/// "Записать пакеты": for a few minutes every frame the game sends is written to Logs/packets-{start}.txt, one per line
/// (time, opcode, length, bytes in hex), so packets the meter does not read yet — the party finder's applicants, for one —
/// can be worked out from a real capture. Nothing leaves the computer; the file may hold names and chat.
/// </summary>
public sealed class PacketRecorder(string dir)
{
    private const int MaxLines = 400_000;

    private readonly Lock gate = new();
    private StreamWriter? writer;
    private Timer? stopTimer;
    private volatile bool recording;

    public bool Recording => recording;

    public DateTime? Until { get; private set; }

    public string? File { get; private set; }

    public int Count { get; private set; }

    public event Action? Changed;

    public void Start(DateTime now, TimeSpan duration)
    {
        lock (gate)
        {
            Close();
            Directory.CreateDirectory(dir);
            File = Path.Combine(dir, $"packets-{now:yyyyMMdd-HHmmss}.txt");
            writer = new StreamWriter(File, append: false) { AutoFlush = false };
            writer.WriteLine($"# AION2 overlay: packets from {now:yyyy-MM-dd HH:mm:ss}, {duration.TotalMinutes:0} min. Time, opcode (LE), length, bytes.");
            Count = 0;
            Until = now + duration;
            recording = true;
            stopTimer = new Timer(_ => Stop(), null, duration + TimeSpan.FromSeconds(1), Timeout.InfiniteTimeSpan);
        }
        Changed?.Invoke();
    }

    public void Stop()
    {
        lock (gate)
        {
            if (!recording) return;
            Close();
        }
        Changed?.Invoke();
    }

    public void Record(DateTime at, ushort opcode, ReadOnlySpan<byte> data)
    {
        if (!recording) return;
        var stop = false;
        lock (gate)
        {
            if (writer is null) return;
            if (at > Until || Count >= MaxLines) stop = true;
            else
            {
                writer.Write(at.ToString("HH:mm:ss.fff"));
                writer.Write(' ');
                writer.Write(opcode.ToString("X4"));
                writer.Write(' ');
                writer.Write(data.Length);
                writer.Write(' ');
                writer.WriteLine(Convert.ToHexString(data));
                Count++;
            }
        }
        if (stop) Stop();
    }

    private void Close()
    {
        recording = false;
        stopTimer?.Dispose();
        stopTimer = null;
        try { writer?.Dispose(); } catch (IOException) { }
        writer = null;
    }
}
