using System.Text.Json;
using System.Text.Json.Serialization;

namespace AionDpsMeter.Timers.Feed;

public sealed class TimersOptions
{
    public int BossLeadMinutes { get; set; } = 5;
    public int RiftLeadMinutes { get; set; } = 2;
    public int EventLeadMinutes { get; set; } = 5;
    public bool Sound { get; set; } = true;

    /// <summary>Toggles the overlay's edit mode. Upstream's hotkey syntax ("Ctrl+Shift+F9"); empty turns it off.</summary>
    public string EditHotkey { get; set; } = "Ctrl+Shift+F9";
    public HashSet<FeedKind> HiddenKinds { get; set; } = [];
    public HashSet<string> HiddenIds { get; set; } = [];

    public int LeadFor(FeedKind kind) => kind switch
    {
        FeedKind.Boss => BossLeadMinutes,
        FeedKind.Rift => RiftLeadMinutes,
        _ => EventLeadMinutes,
    };

    public bool Shows(FeedItem item) => !HiddenKinds.Contains(item.Kind) && !HiddenIds.Contains(item.Id);
}

/// <summary>timers-settings.json next to the exe; edit it by hand to change lead times or sound.</summary>
public sealed class TimersOptionsStore(string path)
{
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public TimersOptions Current { get; private set; } = new();

    public void Load()
    {
        try
        {
            Current = File.Exists(path) ? JsonSerializer.Deserialize<TimersOptions>(File.ReadAllText(path), Json) ?? new() : new();
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            Current = new();
        }
    }

    /// <summary>At startup: load, and write the defaults only when there is no file yet — never over a file the
    /// user edited by hand, even one that no longer parses.</summary>
    public void LoadOrCreate()
    {
        Load();
        if (!File.Exists(path)) Save();
    }

    public void Save()
    {
        try
        {
            File.WriteAllText(path, JsonSerializer.Serialize(Current, Json));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }
}
