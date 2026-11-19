// aion2-overlay fork: new file (see FORK_CHANGES.md).
namespace AionDpsMeter.Timers.Modes;

/// <param name="FirstHit">After the fight's first hit; null for a player who never hit.</param>
/// <param name="SelfHeal">The part of <paramref name="HealReceived"/> the player healed on themselves.</param>
public sealed record FightPlayer(long Id, long Damage, long HealDone, long HealReceived, long SelfHeal, long DamageTaken, int Deaths,
    long MaxHit, string MaxHitSkill, TimeSpan? FirstHit);

public sealed record FightSummary(DateTime Start, TimeSpan Duration, IReadOnlyList<FightPlayer> Players)
{
    public long TotalDamage => Players.Sum(p => p.Damage);
    public long TotalHeal => Players.Sum(p => p.HealDone);
    public long TotalTaken => Players.Sum(p => p.DamageTaken);
    public int TotalDeaths => Players.Sum(p => p.Deaths);
    public double GroupDps => Duration.TotalSeconds > 0 ? TotalDamage / Duration.TotalSeconds : TotalDamage;
}

/// <summary>
/// The fight summary ("Итоги"): who dealt, healed and took how much, who died, the strongest hit and who opened. A fight
/// starts with a player's hit on a monster and ends after the idle time without one; the last fight stays until the next.
/// </summary>
public sealed class FightLog(TimeSpan idle)
{
    private sealed class Row
    {
        public long Damage, HealDone, HealReceived, SelfHeal, Taken, MaxHit;
        public int Deaths;
        public string MaxHitSkill = "";
        public DateTime? FirstHit;
    }

    private readonly Lock gate = new();
    private readonly Dictionary<long, Row> rows = [];
    private DateTime? start;
    private DateTime last;

    public void AddDamage(long player, long damage, string skill, DateTime at)
    {
        lock (gate)
        {
            if (start is null || at - last > idle)
            {
                rows.Clear();
                start = at;
            }
            var r = Get(player);
            r.Damage += damage;
            r.FirstHit ??= at;
            if (damage > r.MaxHit) (r.MaxHit, r.MaxHitSkill) = (damage, skill);
            Touch(at);
        }
    }

    public void AddHeal(long caster, long target, long amount, DateTime at)
    {
        lock (gate)
        {
            if (!Active(at)) return;
            Get(caster).HealDone += amount;
            var t = Get(target);
            t.HealReceived += amount;
            if (caster == target) t.SelfHeal += amount;
            Touch(at);
        }
    }

    public void AddTaken(long player, long damage, DateTime at)
    {
        lock (gate)
        {
            if (!Active(at)) return;
            Get(player).Taken += damage;
            Touch(at);
        }
    }

    public void Died(long player, DateTime at)
    {
        lock (gate)
        {
            if (!Active(at)) return;
            Get(player).Deaths++;
            Touch(at);
        }
    }

    /// <summary>The "clear meter" and "reset fight" hotkeys.</summary>
    public void Clear()
    {
        lock (gate)
        {
            rows.Clear();
            start = null;
        }
    }

    /// <summary>The current fight, or the last one; null before the first.</summary>
    public FightSummary? Summary()
    {
        lock (gate)
        {
            if (start is not { } s) return null;
            var players = rows.Select(kv => new FightPlayer(kv.Key, kv.Value.Damage, kv.Value.HealDone, kv.Value.HealReceived, kv.Value.SelfHeal,
                    kv.Value.Taken, kv.Value.Deaths, kv.Value.MaxHit, kv.Value.MaxHitSkill, kv.Value.FirstHit - s))
                .OrderByDescending(p => p.Damage)
                .ToList();
            return new FightSummary(s, last - s, players);
        }
    }

    private bool Active(DateTime at) => start is not null && at - last <= idle && at >= start;

    private void Touch(DateTime at)
    {
        if (at > last) last = at;
    }

    private Row Get(long id)
    {
        if (!rows.TryGetValue(id, out var r)) rows[id] = r = new Row();
        return r;
    }
}
