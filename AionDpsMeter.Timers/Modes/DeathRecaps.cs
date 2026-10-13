namespace AionDpsMeter.Timers.Modes;

/// <summary>A hit taken by a player, with names resolved when it happened (the attacker may be gone later).</summary>
public sealed record IncomingHit(long TargetId, string Source, string Skill, long Damage, bool Critical, DateTime At);

public sealed record DeathRecap(long PlayerId, DateTime At, IReadOnlyList<IncomingHit> Hits)
{
    public long Total => Hits.Sum(h => h.Damage);
    public IncomingHit? KillingBlow => Hits.Count > 0 ? Hits[^1] : null;
}

/// <summary>The last six seconds of hits before each player's death (LOXIN's death recap).</summary>
public sealed class DeathRecaps
{
    public static readonly TimeSpan Window = TimeSpan.FromSeconds(6);
    private static readonly TimeSpan Keep = TimeSpan.FromSeconds(30);

    private readonly Lock gate = new();
    private readonly List<IncomingHit> recent = [];
    private readonly Dictionary<long, DeathRecap> last = [];

    public event Action<DeathRecap>? Recorded;

    public void Add(IncomingHit hit)
    {
        if (hit.Damage <= 0) return;
        lock (gate)
        {
            recent.Add(hit);
            if (recent.Count > 2048 || recent[0].At < hit.At - Keep * 4)
                recent.RemoveAll(h => h.At < hit.At - Keep);
        }
    }

    public void Died(long playerId, DateTime at)
    {
        DeathRecap recap;
        lock (gate)
        {
            var hits = recent.Where(h => h.TargetId == playerId && h.At <= at && h.At >= at - Window).OrderBy(h => h.At).ToList();
            recap = new DeathRecap(playerId, at, hits);
            last[playerId] = recap;
        }
        Recorded?.Invoke(recap);
    }

    public DeathRecap? LastFor(long playerId)
    {
        lock (gate) return last.GetValueOrDefault(playerId);
    }
}
