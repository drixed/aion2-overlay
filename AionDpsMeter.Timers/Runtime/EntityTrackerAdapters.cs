using AionDpsMeter.Core.Data;
using AionDpsMeter.Services.Services.Entity;

namespace AionDpsMeter.Timers.Runtime;

// EntityTracker uses plain dictionaries written by the packet thread; reads from here can race, so they are
// guarded and simply retried on the next tick.

/// <summary>
/// The user's server, from the server name upstream sets for its own character (ServerId is only filled in through
/// party data, so solo players would always read 0). After a character switch several players carry IsUser; the
/// newest one is the character being played.
/// </summary>
public sealed class EntityTrackerServerContext(EntityTracker entities) : IServerContext
{
    private int last;

    public int CurrentServerId
    {
        get
        {
            try
            {
                var user = entities.PlayerEntities
                    .Where(p => p.IsUser && p.ServerName.Length > 0)
                    .MaxBy(p => p.CreatedAt);
                var id = user is null ? 0 : IdOf(user.ServerName);
                if (id != 0) last = id;
            }
            catch (Exception) { }
            return last;
        }
    }

    /// <summary>Server abbreviations repeat across regions; the lowest id is picked so a name always maps to one id.</summary>
    private static int IdOf(string serverName) =>
        ServerMap.Servers.Where(kv => kv.Value == serverName).Select(kv => kv.Key).DefaultIfEmpty(0).Min();
}

public sealed class EntityTrackerHpSource(EntityTracker entities) : IMobHpSource
{
    public IReadOnlyList<MobHp> Snapshot() =>
        entities.TargetEntities.Select(m => new MobHp(m.Id, m.MobCode, m.HpTotal, m.HpCurrent)).ToList();
}
