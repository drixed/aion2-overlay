namespace AionDpsMeter.Timers.Schedule;

/// <summary>schedule.json from GitHub, else the last downloaded copy, else the copy built into the app.</summary>
public sealed class ScheduleSource(HttpClient http, Uri remote, string cachePath, Func<string> embedded)
{
    public const string DefaultRemote = "https://raw.githubusercontent.com/drixed/aion2-overlay/master/schedule.json";

    private volatile ScheduleData current = ScheduleData.Empty;

    public ScheduleData Current => current;

    /// <summary>"none", "embedded", "cache" or "remote".</summary>
    public string Origin { get; private set; } = "none";

    public event Action? Changed;

    public void LoadLocal()
    {
        try
        {
            if (File.Exists(cachePath))
            {
                current = ScheduleJson.Parse(File.ReadAllText(cachePath));
                Origin = "cache";
                Changed?.Invoke();
                return;
            }
        }
        catch (Exception ex) when (ex is IOException or FormatException or UnauthorizedAccessException) { }

        current = ScheduleJson.Parse(embedded());
        Origin = "embedded";
        Changed?.Invoke();
    }

    public async Task<bool> RefreshAsync(CancellationToken ct = default)
    {
        try
        {
            var json = await http.GetStringAsync(remote, ct);
            var data = ScheduleJson.Parse(json); // throws before anything is overwritten
            var tmp = cachePath + ".tmp";
            await File.WriteAllTextAsync(tmp, json, ct);
            File.Move(tmp, cachePath, overwrite: true);
            current = data;
            Origin = "remote";
            Changed?.Invoke();
            return true;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or FormatException
                                       or IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    public static string ReadEmbedded() => EmbeddedResources.Read("schedule.json");
}
