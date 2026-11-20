// aion2-overlay fork: new file (see FORK_CHANGES.md).
using System.Text.Json;

namespace AionDpsMeter.Timers.Modes;

/// <summary>
/// The fight summaries (healing, damage taken, "Итоги") kept on disk in fight-history.json, so the history window has them
/// for fights before a restart too. A fight is known by its start: a later look at a fight still going replaces it.
/// </summary>
public static class FightArchive
{
    public const int Kept = 500;

    /// <summary>The kept fights with the fresh ones in (the fresh version of a fight wins), newest first, at most <see cref="Kept"/>.</summary>
    public static List<FightSummary> Merge(IEnumerable<FightSummary> kept, IEnumerable<FightSummary> fresh)
    {
        var byStart = kept.ToDictionary(f => f.Start);
        foreach (var f in fresh) byStart[f.Start] = f;
        return byStart.Values.OrderByDescending(f => f.Start).Take(Kept).ToList();
    }

    /// <summary>The fight that overlaps a history session (30 s either way), like the history window matched the live ones.</summary>
    public static FightSummary? Find(IEnumerable<FightSummary> kept, DateTime sessionStart, DateTime sessionEnd) =>
        kept.FirstOrDefault(f => f.Start <= sessionEnd.AddSeconds(30) && f.Start + f.Duration >= sessionStart.AddSeconds(-30));

    /// <summary>The fight with only the group in it: the players of the history session (they hit the boss) and whoever
    /// healed (a healer may not hit at all; monsters do not heal). The boss the group hit and adds counted as "taking"
    /// damage are left out.</summary>
    public static FightSummary OnlyGroup(FightSummary fight, IReadOnlySet<long> group) =>
        fight with { Players = fight.Players.Where(p => group.Contains(p.Id) || p.HealDone > 0).ToList() };

    public static string Serialize(IReadOnlyList<FightSummary> fights) => JsonSerializer.Serialize(fights);

    public static List<FightSummary> Parse(string json)
    {
        try { return JsonSerializer.Deserialize<List<FightSummary>>(json) ?? []; }
        catch (JsonException) { return []; }
        catch (NotSupportedException) { return []; }
    }
}
