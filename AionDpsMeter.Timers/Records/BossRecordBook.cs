namespace AionDpsMeter.Timers.Records;

/// <param name="MyDamage">The user's own damage in the fight (0 when the user did not hit).</param>
public sealed record FightResult(string Boss, long HpTotal, long TotalDamage, TimeSpan Duration, long MyDamage, DateTime EndedAt);

public enum RecordNews { None, FirstKill, FasterKill, BetterMyDps }

/// <summary>A boss's records on one difficulty (the same boss has other HP on another difficulty).</summary>
public sealed class BossRecord
{
    public string Boss { get; set; } = "";
    public long HpTotal { get; set; }
    public int Kills { get; set; }
    public TimeSpan BestKill { get; set; }
    public DateTime BestKillAt { get; set; }
    /// <summary>The time record before the current one, for "−0:12" on a new record; null until it was beaten.</summary>
    public TimeSpan? PreviousBestKill { get; set; }
    public double BestPartyDps { get; set; }
    public double BestMyDps { get; set; }
    public TimeSpan LastKill { get; set; }
    public DateTime LastKillAt { get; set; }
}

/// <summary>Best kill time, party DPS and own DPS for every boss the user killed.</summary>
public sealed class BossRecordBook
{
    /// <summary>A kill: the party dealt the boss's HP, give or take overkill (history: 100–100.3%).</summary>
    private const double KillShare = 0.95, OverkillShare = 1.2;
    /// <summary>Ordinary mobs have at most ~300K HP, the smallest dungeon boss seen 900K.</summary>
    private const long BossHp = 500_000;
    /// <summary>Shorter means the meter caught only the end of the fight.</summary>
    private static readonly TimeSpan MinimumFight = TimeSpan.FromSeconds(10);

    private readonly Dictionary<string, BossRecord> records = new(StringComparer.Ordinal);

    public IReadOnlyCollection<BossRecord> All => records.Values;

    public BossRecord? For(string boss, long hpTotal) => records.GetValueOrDefault(Key(boss, hpTotal));

    public static bool IsKill(FightResult f) =>
        !string.IsNullOrWhiteSpace(f.Boss)
        && !f.Boss.StartsWith("Unknown", StringComparison.OrdinalIgnoreCase)
        && !f.Boss.Contains("Scarecrow", StringComparison.OrdinalIgnoreCase)
        && f.HpTotal >= BossHp
        && f.TotalDamage >= f.HpTotal * KillShare
        && f.TotalDamage <= f.HpTotal * OverkillShare
        && f.Duration >= MinimumFight;

    public RecordNews Add(FightResult f)
    {
        if (!IsKill(f)) return RecordNews.None;
        var seconds = f.Duration.TotalSeconds;
        var partyDps = f.TotalDamage / seconds;
        var myDps = f.MyDamage / seconds;
        var key = Key(f.Boss, f.HpTotal);

        if (!records.TryGetValue(key, out var r))
        {
            records[key] = new BossRecord
            {
                Boss = f.Boss, HpTotal = f.HpTotal, Kills = 1,
                BestKill = f.Duration, BestKillAt = f.EndedAt, BestPartyDps = partyDps, BestMyDps = myDps,
                LastKill = f.Duration, LastKillAt = f.EndedAt,
            };
            return RecordNews.FirstKill;
        }

        r.Kills++;
        r.LastKill = f.Duration;
        r.LastKillAt = f.EndedAt;
        r.BestPartyDps = Math.Max(r.BestPartyDps, partyDps);
        var news = RecordNews.None;
        if (myDps > r.BestMyDps)
        {
            r.BestMyDps = myDps;
            news = RecordNews.BetterMyDps;
        }
        if (f.Duration < r.BestKill)
        {
            r.PreviousBestKill = r.BestKill;
            r.BestKill = f.Duration;
            r.BestKillAt = f.EndedAt;
            news = RecordNews.FasterKill; // the bigger news wins
        }
        return news;
    }

    public void Import(IEnumerable<BossRecord> saved)
    {
        records.Clear();
        foreach (var r in saved) records[Key(r.Boss, r.HpTotal)] = r;
    }

    public void Clear() => records.Clear();

    private static string Key(string boss, long hpTotal) => $"{boss}|{hpTotal}";
}

public static class BossPace
{
    /// <summary>Projected kill time (elapsed + time to kill at the current DPS) minus the record: negative is faster.
    /// Null without a record or an estimate.</summary>
    public static TimeSpan? Delta(BossRecord? record, TimeSpan elapsed, TimeSpan? toKill) =>
        record is null || toKill is not { } left ? null : elapsed + left - record.BestKill;
}

public static class RecordText
{
    /// <summary>Kill times are truncated to whole seconds, like the fight timer.</summary>
    public static string Time(TimeSpan t) => AionDpsMeter.Timers.Feed.FeedText.Span(TimeSpan.FromSeconds(Math.Floor(t.TotalSeconds)));

    public static string Pace(TimeSpan delta) =>
        Math.Abs(delta.TotalSeconds) < 1 ? "в темпе рекорда"
        : delta < TimeSpan.Zero ? $"быстрее рекорда на {Time(-delta)}"
        : $"медленнее рекорда на {Time(delta)}";
}
