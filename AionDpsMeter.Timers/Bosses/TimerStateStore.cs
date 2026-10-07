using System.Text.Json;

namespace AionDpsMeter.Timers.Bosses;

/// <summary>timers-state.json next to the exe; written atomically so a crash mid-save leaves the old file.</summary>
public sealed class TimerStateStore(string path)
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public TrackerState? Load()
    {
        try
        {
            return File.Exists(path) ? JsonSerializer.Deserialize<TrackerState>(File.ReadAllText(path), Options) : null;
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    public void Save(TrackerState state)
    {
        try
        {
            var tmp = path + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(state, Options));
            File.Move(tmp, path, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }
}
