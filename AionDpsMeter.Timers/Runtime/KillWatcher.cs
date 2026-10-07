using AionDpsMeter.Timers.Bosses;

namespace AionDpsMeter.Timers.Runtime;

/// <summary>
/// Polls the meter's mob table once a second: a boss seen with HP that then drops to 0 is a kill.
/// Upstream's death packet is handled by upstream (one processor per opcode), so this reads the HP it already tracks.
/// </summary>
public sealed class KillWatcher(IMobHpSource source, IServerContext server, BossCatalog catalog, FieldBossTracker tracker)
{
    private readonly HashSet<int> alive = new();

    public void Tick()
    {
        IReadOnlyList<MobHp> mobs;
        try
        {
            mobs = source.Snapshot();
        }
        catch (Exception)
        {
            return; // the packet thread changed the table mid-read: try again next tick
        }

        var present = new HashSet<int>();
        foreach (var m in mobs)
        {
            present.Add(m.EntityId);
            if (m.MobCode == 0 || !catalog.IsBoss(m.MobCode)) continue;
            if (m.HpCurrent > 0) alive.Add(m.EntityId);
            else if (alive.Remove(m.EntityId)) tracker.OnKill(server.CurrentServerId, m.MobCode);
        }
        alive.IntersectWith(present);
    }
}
