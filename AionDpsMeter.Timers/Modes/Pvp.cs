namespace AionDpsMeter.Timers.Modes;

/// <param name="Mine">The user is the attacker or the victim.</param>
public sealed record PvpHit(long AttackerId, long VictimId, int SkillId, string Skill, long Damage, bool Critical, DateTime At, bool Mine);

public sealed record PvpSkillRow(string Skill, long Total, int Hits, int Crits);

public sealed record PvpReceivedRow(long AttackerId, string Skill, long Total, int Hits);

/// <param name="Active">PvP hits within the idle time; otherwise the last fight is shown until the next one.</param>
public sealed record PvpView(IReadOnlyList<ModeRow> Rows, TimeSpan Elapsed, bool Active, bool HasFight);

/// <summary>
/// Player-on-player hits (arena, open world), one fight at a time: it starts with a PvP hit and ends after the idle time
/// without one; the last fight stays for a look after it until the next starts. Rows are damage dealt to players.
/// </summary>
public sealed class PvpLog(TimeSpan idle)
{
    private readonly Lock gate = new();
    private readonly List<PvpHit> fight = [];
    private DateTime lastMine = DateTime.MinValue;

    public void Add(PvpHit hit)
    {
        lock (gate)
        {
            if (fight.Count > 0 && hit.At - fight[^1].At > idle) fight.Clear();
            fight.Add(hit);
            if (hit.Mine) lastMine = hit.At;
        }
    }

    /// <summary>A PvP hit dealt or taken by the user within the idle time: the overlay switches to the PvP mode.</summary>
    public bool MineActive(DateTime now)
    {
        lock (gate) return now - lastMine <= idle;
    }

    public PvpView View(DateTime now)
    {
        lock (gate)
        {
            if (fight.Count == 0) return new PvpView([], TimeSpan.Zero, false, false);
            var elapsed = fight[^1].At - fight[0].At;
            var seconds = Math.Max(1, elapsed.TotalSeconds);
            var rows = fight.GroupBy(h => h.AttackerId)
                .Select(g => new ModeRow(g.Key, g.Sum(h => h.Damage), g.Sum(h => h.Damage) / seconds))
                .OrderByDescending(r => r.Total)
                .ToList();
            return new PvpView(rows, elapsed, now - fight[^1].At <= idle, true);
        }
    }

    /// <summary>What the player hit other players with in the fight, biggest first.</summary>
    public IReadOnlyList<PvpSkillRow> Dealt(long player)
    {
        lock (gate)
            return fight.Where(h => h.AttackerId == player)
                .GroupBy(h => h.Skill)
                .Select(g => new PvpSkillRow(g.Key, g.Sum(h => h.Damage), g.Count(), g.Count(h => h.Critical)))
                .OrderByDescending(r => r.Total)
                .ToList();
    }

    /// <summary>Who hit the player with what in the fight, biggest first.</summary>
    public IReadOnlyList<PvpReceivedRow> Received(long player)
    {
        lock (gate)
            return fight.Where(h => h.VictimId == player)
                .GroupBy(h => (h.AttackerId, h.Skill))
                .Select(g => new PvpReceivedRow(g.Key.AttackerId, g.Key.Skill, g.Sum(h => h.Damage), g.Count()))
                .OrderByDescending(r => r.Total)
                .ToList();
    }
}
