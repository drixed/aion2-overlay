namespace AionDpsMeter.Timers.Modes;

/// <param name="Mine">The user is the attacker or the victim.</param>
/// <param name="Icon">The skill icon's URL from the game data, when it has one.</param>
public sealed record PvpHit(long AttackerId, long VictimId, int SkillId, string Skill, long Damage, bool Critical, DateTime At, bool Mine, string? Icon = null);

/// <summary>A player killed by another: the last PvP hit on them within 5 s of the death.</summary>
public sealed record PvpKill(long KillerId, long VictimId, string Skill, string? Icon, DateTime At);

public sealed record PvpSkillRow(string Skill, long Total, int Hits, int Crits, string? Icon = null);

public sealed record PvpReceivedRow(long AttackerId, string Skill, long Total, int Hits, string? Icon = null);

/// <param name="Active">PvP hits within the idle time; otherwise the last fight is shown until the next one.</param>
public sealed record PvpView(IReadOnlyList<ModeRow> Rows, TimeSpan Elapsed, bool Active, bool HasFight);

/// <summary>
/// Player-on-player hits (arena, open world), one fight at a time: it starts with a PvP hit and ends after the idle time
/// without one; the last fight stays for a look after it until the next starts. Rows are damage dealt to players.
/// </summary>
public sealed class PvpLog(TimeSpan idle)
{
    private readonly Lock gate = new();
    private static readonly TimeSpan KillWindow = TimeSpan.FromSeconds(5);
    private readonly List<PvpHit> fight = [];
    private readonly List<PvpKill> kills = [];
    private DateTime lastMine = DateTime.MinValue;

    /// <summary>A PvP target: a player the game named (or the user), not one the game sent as a monster, not a summon.
    /// Monsters casting skills with player-like codes become unnamed "Player_…" entries and are not targets.</summary>
    public static bool IsPlayerTarget(AionDpsMeter.Core.Models.Player? victim, AionDpsMeter.Core.Models.Mob? asMob, bool summon) =>
        victim is { } v && (v.IsIdentified || v.IsUser) && asMob is not { MobCode: > 0 } && !summon;

    public void Add(PvpHit hit)
    {
        lock (gate)
        {
            if (fight.Count > 0 && hit.At - fight[^1].At > idle)
            {
                fight.Clear();
                kills.Clear();
            }
            fight.Add(hit);
            if (hit.Mine) lastMine = hit.At;
        }
    }

    /// <summary>The "clear meter" and "reset fight" hotkeys: the fight is gone and the window leaves the PvP mode.</summary>
    public void Clear()
    {
        lock (gate)
        {
            fight.Clear();
            kills.Clear();
            lastMine = DateTime.MinValue;
        }
    }

    /// <summary>A player died: a PvP kill when another player hit them within 5 s before.</summary>
    public void Died(long victim, DateTime at)
    {
        lock (gate)
        {
            if (fight.LastOrDefault(h => h.VictimId == victim) is { } last && at - last.At <= KillWindow && at >= last.At)
                kills.Add(new PvpKill(last.AttackerId, victim, last.Skill, last.Icon, at));
        }
    }

    /// <summary>Kills in the fight, newest first.</summary>
    public IReadOnlyList<PvpKill> Kills()
    {
        lock (gate) return kills.AsEnumerable().Reverse().ToList();
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
                .Select(g => new PvpSkillRow(g.Key, g.Sum(h => h.Damage), g.Count(), g.Count(h => h.Critical), g.Select(h => h.Icon).FirstOrDefault(i => i is not null)))
                .OrderByDescending(r => r.Total)
                .ToList();
    }

    /// <summary>Who hit the player with what in the fight, biggest first.</summary>
    public IReadOnlyList<PvpReceivedRow> Received(long player)
    {
        lock (gate)
            return fight.Where(h => h.VictimId == player)
                .GroupBy(h => (h.AttackerId, h.Skill))
                .Select(g => new PvpReceivedRow(g.Key.AttackerId, g.Key.Skill, g.Sum(h => h.Damage), g.Count(), g.Select(h => h.Icon).FirstOrDefault(i => i is not null)))
                .OrderByDescending(r => r.Total)
                .ToList();
    }
}
