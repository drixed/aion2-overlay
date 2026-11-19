// aion2-overlay fork: new file (see FORK_CHANGES.md).
namespace AionDpsMeter.Timers.Modes;

/// <param name="FirstHit">After the fight's first hit; null for a player who never hit.</param>
/// <param name="SelfHeal">The part of <paramref name="HealReceived"/> the player healed on themselves.</param>
public sealed record FightPlayer(long Id, long Damage, long HealDone, long HealReceived, long SelfHeal, long DamageTaken, int Deaths,
    long MaxHit, string MaxHitSkill, TimeSpan? FirstHit);

/// <param name="Target">The monster the group hit hardest — the fight's name in the list.</param>
public sealed record FightSummary(DateTime Start, TimeSpan Duration, IReadOnlyList<FightPlayer> Players, string Target = "")
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
    private const int Kept = 20;

    private sealed class Row
    {
        public long Damage, HealDone, HealReceived, SelfHeal, Taken, MaxHit;
        public int Deaths;
        public string MaxHitSkill = "";
        public DateTime? FirstHit;
    }

    private sealed class Fight(DateTime start)
    {
        public readonly DateTime Start = start;
        public DateTime Last = start;
        public readonly Dictionary<long, Row> Rows = [];
        public readonly Dictionary<string, long> Targets = [];
    }

    private readonly Lock gate = new();
    private readonly List<Fight> fights = [];

    private Fight? Current => fights.Count > 0 ? fights[^1] : null;

    public void AddDamage(long player, long damage, string skill, DateTime at, string? target = null)
    {
        lock (gate)
        {
            if (Current is not { } f || at - f.Last > idle)
            {
                fights.Add(f = new Fight(at));
                if (fights.Count > Kept) fights.RemoveAt(0);
            }
            var r = Get(f, player);
            r.Damage += damage;
            r.FirstHit ??= at;
            if (damage > r.MaxHit) (r.MaxHit, r.MaxHitSkill) = (damage, skill);
            if (!string.IsNullOrEmpty(target)) f.Targets[target] = f.Targets.GetValueOrDefault(target) + damage;
            Touch(f, at);
        }
    }

    public void AddHeal(long caster, long target, long amount, DateTime at)
    {
        lock (gate)
        {
            if (Active(at) is not { } f) return;
            Get(f, caster).HealDone += amount;
            var t = Get(f, target);
            t.HealReceived += amount;
            if (caster == target) t.SelfHeal += amount;
            Touch(f, at);
        }
    }

    public void AddTaken(long player, long damage, DateTime at)
    {
        lock (gate)
        {
            if (Active(at) is not { } f) return;
            Get(f, player).Taken += damage;
            Touch(f, at);
        }
    }

    public void Died(long player, DateTime at)
    {
        lock (gate)
        {
            if (Active(at) is not { } f) return;
            Get(f, player).Deaths++;
            Touch(f, at);
        }
    }

    /// <summary>The "clear meter" and "reset fight" hotkeys: every kept fight goes.</summary>
    public void Clear()
    {
        lock (gate) fights.Clear();
    }

    /// <summary>The current fight, or the last one; null before the first.</summary>
    public FightSummary? Summary()
    {
        lock (gate) return Current is { } f ? Summarize(f) : null;
    }

    /// <summary>The kept fights (the last 20), newest first.</summary>
    public IReadOnlyList<FightSummary> History()
    {
        lock (gate) return fights.AsEnumerable().Reverse().Select(Summarize).ToList();
    }

    private static FightSummary Summarize(Fight f)
    {
        var players = f.Rows.Select(kv => new FightPlayer(kv.Key, kv.Value.Damage, kv.Value.HealDone, kv.Value.HealReceived, kv.Value.SelfHeal,
                kv.Value.Taken, kv.Value.Deaths, kv.Value.MaxHit, kv.Value.MaxHitSkill, kv.Value.FirstHit - f.Start))
            .OrderByDescending(p => p.Damage)
            .ToList();
        var target = f.Targets.Count > 0 ? f.Targets.MaxBy(t => t.Value).Key : "";
        return new FightSummary(f.Start, f.Last - f.Start, players, target);
    }

    private Fight? Active(DateTime at) => Current is { } f && at - f.Last <= idle && at >= f.Start ? f : null;

    private static void Touch(Fight f, DateTime at)
    {
        if (at > f.Last) f.Last = at;
    }

    private static Row Get(Fight f, long id)
    {
        if (!f.Rows.TryGetValue(id, out var r)) f.Rows[id] = r = new Row();
        return r;
    }
}
