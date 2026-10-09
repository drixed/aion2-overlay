using System.Text.Json;
using System.Text.Json.Serialization;
using AionDpsMeter.Timers.Overlay;

namespace AionDpsMeter.Timers.Feed;

public sealed class TimersOptions
{
    public int BossLeadMinutes { get; set; } = 5;
    public int RiftLeadMinutes { get; set; } = 2;
    public int EventLeadMinutes { get; set; } = 5;
    public bool Sound { get; set; } = true;

    /// <summary>The timers window is folded down to its header.</summary>
    public bool Collapsed { get; set; }

    /// <summary>Height to restore when the folded window opens again.</summary>
    public double ExpandedHeight { get; set; } = 460;

    /// <summary>Notice ids the user answered «Понятно» to.</summary>
    public HashSet<string> DismissedNotices { get; set; } = [];

    public bool IsDismissed(string noticeId) => DismissedNotices.Contains(noticeId);
    // Overlay settings (the ✦ window), modelled on the Abyss meter's.
    public DpsMode DpsMode { get; set; } = DpsMode.Effective;
    public bool HideTotalDamage { get; set; }
    /// <summary>Rows, shares, party DPS and the summary count only me and my party.</summary>
    public bool OnlyMyParty { get; set; }
    public bool ShowPartyDps { get; set; } = true;
    public bool ShowEnrage { get; set; } = true;
    public bool SummaryMultiline { get; set; }
    /// <summary>In a fight, hide the header, notice and footer; they come back under the mouse.</summary>
    public bool FocusMode { get; set; }
    /// <summary>Topmost only while AION 2 is the foreground window.</summary>
    public bool OnlyOverGame { get; set; }
    /// <summary>Percent, 80–150.</summary>
    public int UiScale { get; set; } = 100;
    // Global hotkeys, as in the Abyss meter. Empty = not set.
    public string HideOverlayHotkey { get; set; } = "";
    /// <summary>Ends the current fight (it goes to the history) so the meter starts fresh.</summary>
    public string ResetFightHotkey { get; set; } = "";
    /// <summary>Clears the meter completely.</summary>
    public string ClearMeterHotkey { get; set; } = "";
    public string CopySummaryHotkey { get; set; } = "";
    public string CompactHotkey { get; set; } = "";
    public string ClickThroughHotkey { get; set; } = "";

    /// <summary>The main window shows only its header and my own row.</summary>
    public bool Compact { get; set; }

    /// <summary>Next to a player's name in the main window: БМ, ГС, both or nothing.</summary>
    public AionDpsMeter.Timers.Overlay.PowerColumn PowerColumn { get; set; } = AionDpsMeter.Timers.Overlay.PowerColumn.CombatPower;
    /// <summary>The field boss timers window is shown.</summary>
    public bool ShowBossTimers { get; set; } = true;

    /// <summary>Show the fight card for ordinary mobs too, not only bosses.</summary>
    public bool CardForAllTargets { get; set; }

    /// <summary>"boss" — the fork's boss panel as the main window; "upstream" — RATmeter's own style.</summary>
    public string MainStyle { get; set; } = "boss";

    [JsonIgnore]
    public bool UseBossPanel => !string.Equals(MainStyle, "upstream", StringComparison.OrdinalIgnoreCase);

    public HashSet<FeedKind> HiddenKinds { get; set; } = [];
    public HashSet<string> HiddenIds { get; set; } = [];

    public int LeadFor(FeedKind kind) => kind switch
    {
        FeedKind.Boss => BossLeadMinutes,
        FeedKind.Rift => RiftLeadMinutes,
        _ => EventLeadMinutes,
    };

    public bool Shows(FeedItem item) => !HiddenKinds.Contains(item.Kind) && !HiddenIds.Contains(item.Id);

    /// <summary>The key in <see cref="MutedIds"/> that silences every field boss at once.</summary>
    public const string AllFieldBosses = "boss";

    /// <summary>Events (schedule ids, or <see cref="AllFieldBosses"/>) the user does not want alerts for; they stay shown.</summary>
    public HashSet<string> MutedIds { get; set; } = [];

    public bool Notifies(FeedItem item) =>
        Shows(item)
        && LeadFor(item.Kind) > 0
        && !MutedIds.Contains(AlertKey(item));

    /// <summary>The key alerts are muted and given a sound by: the schedule event id, or one key for all field bosses.</summary>
    public static string AlertKey(FeedItem item) => item.Kind == FeedKind.Boss ? AllFieldBosses : item.Id;

    /// <summary>Alert keys (see <see cref="AlertKey"/>) to the sound they play, as <see cref="AlertSound"/> text.</summary>
    public Dictionary<string, string> Sounds { get; set; } = [];

    public AlertSound SoundFor(FeedItem item) => SoundFor(AlertKey(item));

    public AlertSound SoundFor(string key) => AlertSound.Parse(Sounds.GetValueOrDefault(key));

    public void SetSound(string key, AlertSound sound)
    {
        if (sound.IsDefault) Sounds.Remove(key);
        else Sounds[key] = sound.ToString();
    }
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

    /// <summary>Raised after every save, so hotkeys and windows pick up new settings at once.</summary>
    public event Action? Changed;

    public void Save()
    {
        try
        {
            File.WriteAllText(path, JsonSerializer.Serialize(Current, Json));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        Changed?.Invoke();
    }
}
