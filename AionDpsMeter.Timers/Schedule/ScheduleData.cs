namespace AionDpsMeter.Timers.Schedule;

/// <summary>A map whose in-game boss list names its bosses by place in this NPC code block (code / 1000).</summary>
public sealed record FieldBossMapInfo(int Block, string Name);

/// <summary>An announcement shown as a banner in the main window (e.g. "the meter is being updated for a new patch").
/// <paramref name="Id"/> changes for every new notice, so a dismissed one never hides the next.</summary>
public sealed record Notice(string Id, string Title, string Text);

public sealed record ScheduleData(
    IReadOnlyList<ScheduledEvent> Events,
    IReadOnlyDictionary<int, FieldBossMapInfo> FieldBossMaps,
    IReadOnlyDictionary<int, int> RespawnMinutes,
    IReadOnlyDictionary<int, int>? EnrageSeconds = null,
    Notice? Notice = null)
{
    private static readonly IReadOnlyDictionary<int, int> None = new Dictionary<int, int>();

    public static ScheduleData Empty { get; } =
        new([], new Dictionary<int, FieldBossMapInfo>(), new Dictionary<int, int>());

    /// <summary>NPC code → enrage timer in seconds; 0 = the boss has none.</summary>
    public IReadOnlyDictionary<int, int> Enrage => EnrageSeconds ?? None;
}
