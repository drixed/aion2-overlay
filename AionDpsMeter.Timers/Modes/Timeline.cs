// aion2-overlay fork: new file (see FORK_CHANGES.md).
namespace AionDpsMeter.Timers.Modes;

/// <summary>The history window's timelines: casts from hits (the opening, the skill timeline) and buff uptime.</summary>
public static class Timeline
{
    private static readonly TimeSpan SameCast = TimeSpan.FromSeconds(0.8);

    /// <summary>One cast per run of hits of the same skill less than 0.8 s apart (multi-hit skills, DoT ticks).</summary>
    public static IReadOnlyList<(DateTime At, int Skill)> Casts(IEnumerable<(DateTime At, int Skill)> hits)
    {
        var casts = new List<(DateTime At, int Skill)>();
        DateTime last = default;
        foreach (var h in hits.OrderBy(h => h.At))
        {
            if (casts.Count > 0 && casts[^1].Skill == h.Skill && h.At - last < SameCast)
            {
                last = h.At;
                continue;
            }
            casts.Add(h);
            last = h.At;
        }
        return casts;
    }

    /// <summary>Buff intervals clipped to the fight and merged where they overlap.</summary>
    public static IReadOnlyList<(DateTime From, DateTime To)> Merge(IEnumerable<(DateTime From, DateTime To)> intervals, DateTime start, DateTime end)
    {
        var merged = new List<(DateTime From, DateTime To)>();
        foreach (var (f, t) in intervals.Select(i => (From: i.From < start ? start : i.From, To: i.To > end ? end : i.To))
                     .Where(i => i.To > i.From).OrderBy(i => i.From))
        {
            if (merged.Count > 0 && f <= merged[^1].To) merged[^1] = (merged[^1].From, t > merged[^1].To ? t : merged[^1].To);
            else merged.Add((f, t));
        }
        return merged;
    }

    /// <summary>The share of the fight the merged intervals cover, 0–1.</summary>
    public static double Uptime(IEnumerable<(DateTime From, DateTime To)> merged, DateTime start, DateTime end) =>
        end > start ? merged.Sum(i => (i.To - i.From).TotalSeconds) / (end - start).TotalSeconds : 0;
}
