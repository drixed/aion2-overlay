namespace AionDpsMeter.Timers.Schedule;

/// <summary>A map whose in-game boss list names its bosses by place in this NPC code block (code / 1000).</summary>
public sealed record FieldBossMapInfo(int Block, string Name);

public sealed record ScheduleData(
    IReadOnlyList<ScheduledEvent> Events,
    IReadOnlyDictionary<int, FieldBossMapInfo> FieldBossMaps,
    IReadOnlyDictionary<int, int> RespawnMinutes)
{
    public static ScheduleData Empty { get; } =
        new([], new Dictionary<int, FieldBossMapInfo>(), new Dictionary<int, int>());
}
