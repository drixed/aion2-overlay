namespace AionDpsMeter.Timers.Schedule;

/// <summary>A map whose in-game boss list names its bosses by place in this NPC code block (code / 1000).</summary>
public sealed record FieldBossMapInfo(int Block, string Name);

public sealed record ScheduleData(
    IReadOnlyList<ScheduledEvent> Events,
    IReadOnlyDictionary<int, FieldBossMapInfo> FieldBossMaps,
    IReadOnlyDictionary<int, int> RespawnMinutes,
    IReadOnlyDictionary<int, int>? EnrageSeconds = null)
{
    private static readonly IReadOnlyDictionary<int, int> None = new Dictionary<int, int>();

    public static ScheduleData Empty { get; } =
        new([], new Dictionary<int, FieldBossMapInfo>(), new Dictionary<int, int>());

    /// <summary>NPC code → enrage timer in seconds; 0 = the boss has none.</summary>
    public IReadOnlyDictionary<int, int> Enrage => EnrageSeconds ?? None;
}
