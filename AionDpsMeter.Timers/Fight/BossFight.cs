namespace AionDpsMeter.Timers.Fight;

/// <param name="ToKill">Remaining HP over the party's DPS; null when it cannot be estimated.</param>
/// <param name="Enrage">The boss's enrage timer; null when it has none.</param>
public sealed record BossFightView(
    string Name,
    long HpCurrent,
    long HpTotal,
    double HpPercent,
    TimeSpan Elapsed,
    TimeSpan? ToKill,
    TimeSpan? Enrage,
    TimeSpan? ToEnrage)
{
    public bool Enraged => Enrage is { } e && Elapsed >= e;
}

public static class BossFight
{
    /// <summary>Most dungeon bosses go berserk after 5:00 of combat (metabot.gg boss pages).</summary>
    public static readonly TimeSpan DefaultEnrage = TimeSpan.FromMinutes(5);

    private static readonly double MaxShownSeconds = (TimeSpan.FromMinutes(100) - TimeSpan.FromSeconds(1)).TotalSeconds;

    public static TimeSpan? EnrageFor(int mobCode, bool isFieldBoss, IReadOnlyDictionary<int, int> overrides) =>
        overrides.TryGetValue(mobCode, out var seconds) ? (seconds > 0 ? TimeSpan.FromSeconds(seconds) : null)
        : isFieldBoss ? null
        : DefaultEnrage;

    public static BossFightView Build(string name, long hpCurrent, long hpTotal, double partyDps, TimeSpan elapsed, TimeSpan? enrage)
    {
        var percent = hpTotal > 0 ? Math.Clamp((double)hpCurrent / hpTotal * 100, 0, 100) : 0;
        TimeSpan? toKill = null;
        if (hpCurrent > 0 && partyDps > 0)
        {
            var seconds = Math.Ceiling(hpCurrent / partyDps);
            if (seconds <= MaxShownSeconds) toKill = TimeSpan.FromSeconds(seconds);
        }
        TimeSpan? toEnrage = enrage is { } e ? (e > elapsed ? e - elapsed : TimeSpan.Zero) : null;
        return new BossFightView(name, hpCurrent, hpTotal, percent, elapsed, toKill, enrage, toEnrage);
    }
}
