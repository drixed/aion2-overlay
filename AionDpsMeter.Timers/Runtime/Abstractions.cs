namespace AionDpsMeter.Timers.Runtime;

/// <summary>The server of the character being played; 0 until it is known.</summary>
public interface IServerContext
{
    int CurrentServerId { get; }
}

public readonly record struct MobHp(int EntityId, int MobCode, long HpTotal, long HpCurrent);

public interface IMobHpSource
{
    IReadOnlyList<MobHp> Snapshot();
}
