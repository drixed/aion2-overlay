using AionDpsMeter.Services.Services.Entity;

namespace AionDpsMeter.Timers.Runtime;

// EntityTracker uses plain dictionaries written by the packet thread; reads from here can race, so they are
// guarded and simply retried on the next tick.

public sealed class EntityTrackerServerContext(EntityTracker entities) : IServerContext
{
    private int last;

    public int CurrentServerId
    {
        get
        {
            try
            {
                var id = entities.PlayerEntities.FirstOrDefault(p => p.IsUser)?.ServerId ?? 0;
                if (id != 0) last = id;
            }
            catch (Exception) { }
            return last;
        }
    }
}

public sealed class EntityTrackerHpSource(EntityTracker entities) : IMobHpSource
{
    public IReadOnlyList<MobHp> Snapshot() =>
        entities.TargetEntities.Select(m => new MobHp(m.Id, m.MobCode, m.HpTotal, m.HpCurrent)).ToList();
}
