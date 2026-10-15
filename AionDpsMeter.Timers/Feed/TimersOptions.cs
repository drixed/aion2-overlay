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
    /// <summary>Shows or hides the boss timers window (the same as its switch in settings).</summary>
    public string ToggleBossTimersHotkey { get; set; } = "";

    /// <summary>The main window shows only its header and my own row.</summary>
    public bool Compact { get; set; }

    /// <summary>The boss card shows the boss's records and the pace against them; a new record gets a banner.</summary>
    public bool ShowRecords { get; set; } = true;

    /// <summary>What the rows show: DPS, total damage, healing on others, a dummy run or a grind session.</summary>
    public AionDpsMeter.Timers.Modes.MeterMode Mode { get; set; } = AionDpsMeter.Timers.Modes.MeterMode.Dps;
    /// <summary>Hitting a training scarecrow shows the dummy run (from DPS or Damage mode).</summary>
    public bool AutoDummy { get; set; } = true;
    /// <summary>Length of a dummy run, seconds.</summary>
    public int DummyRunSeconds { get; set; } = 60;

    // Look of the main window (Appearance), as in the LOXIN meter.
    public AionDpsMeter.Timers.Overlay.BarStyle BarStyle { get; set; } = AionDpsMeter.Timers.Overlay.BarStyle.Classic;
    /// <summary>Bar height, percent (60–160).</summary>
    public int BarHeight { get; set; } = 100;
    public AionDpsMeter.Timers.Overlay.PanelTheme Theme { get; set; } = AionDpsMeter.Timers.Overlay.PanelTheme.Default;
    public AionDpsMeter.Timers.Overlay.PanelFont Font { get; set; } = AionDpsMeter.Timers.Overlay.PanelFont.System;
    /// <summary>Font size, px (10–16).</summary>
    public int FontSize { get; set; } = 13;
    /// <summary>"#rrggbb" or empty for the theme's.</summary>
    public string AccentColor { get; set; } = "";
    /// <summary>"#rrggbb" or empty for the theme's.</summary>
    public string BackgroundColor { get; set; } = "";
    /// <summary>The window grows upward: its bottom edge stays put (for a meter low on the screen).</summary>
    public bool GrowUpward { get; set; }
    public bool FirstPlaceAtBottom { get; set; }
    public bool KeepMeOnTop { get; set; }
    public bool RankNumbers { get; set; }
    /// <summary>The event icons show their countdown all the time, not only shortly before.</summary>
    public bool EventTimesAlways { get; set; }
    /// <summary>Out of combat the main window folds into the compact bar by itself.</summary>
    public bool AutoCompact { get; set; }
    public bool HideBossHpBar { get; set; }
    /// <summary>Window opacity while clicks pass through it (locked over the game), percent; 100 = unchanged.</summary>
    public int LockedOpacity { get; set; } = 100;
    /// <summary>Seconds without hits before a fight ends (upstream: 45).</summary>
    public int FightIdleSeconds { get; set; } = 45;
    /// <summary>Hitting a monster whose pet is not maxed shows its souls; a pet level up gets a banner and a sound.</summary>
    public bool ShowPets { get; set; } = true;

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

    /// <summary>Minutes ahead per event (alert key); events without their own value use their kind's.</summary>
    public Dictionary<string, int> LeadById { get; set; } = [];

    public int LeadFor(FeedItem item) => LeadById.TryGetValue(AlertKey(item), out var minutes) ? minutes : LeadFor(item.Kind);

    public int LeadFor(string key, FeedKind kind) => LeadById.TryGetValue(key, out var minutes) ? minutes : LeadFor(kind);

    public void SetLead(string key, int minutes) => LeadById[key] = Math.Clamp(minutes, 1, 60);

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
        && LeadFor(item) > 0
        && !MutedIds.Contains(AlertKey(item))
        && (item.AlertByDefault || EnabledIds.Contains(AlertKey(item)));

    /// <summary>Events that are quiet by default (schedule "alert": false) and the user switched on.</summary>
    public HashSet<string> EnabledIds { get; set; } = [];

    public void SetAlert(string key, bool on)
    {
        if (on) { MutedIds.Remove(key); EnabledIds.Add(key); }
        else { MutedIds.Add(key); EnabledIds.Remove(key); }
    }

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
