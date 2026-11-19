// aion2-overlay fork: new file (see FORK_CHANGES.md).
namespace AionDpsMeter.Timers.Modes;

/// <summary>The history window's timelines: casts from hits (the opening, the skill timeline) and buff uptime.</summary>
public static class Timeline
{
    private static readonly TimeSpan SameCast = TimeSpan.FromSeconds(0.8);

    /// <summary>One cast per run of hits of the same skill less than 0.8 s apart (multi-hit skills, DoT ticks).</summary>
    public static IReadOnlyList<(DateTime At, int Skill)> Casts(IEnumerable<(DateTime At, int Skill)> hits) =>
        Runs(hits).Select(r => (r.At, r.Skill)).ToList();

    /// <summary>The casts with where each run of hits ended and how many hits it had — a channel shows as a bar.</summary>
    public static IReadOnlyList<(DateTime At, DateTime End, int Skill, int Hits)> Runs(IEnumerable<(DateTime At, int Skill)> hits)
    {
        var runs = new List<(DateTime At, DateTime End, int Skill, int Hits)>();
        foreach (var h in hits.OrderBy(h => h.At))
        {
            if (runs.Count > 0 && runs[^1].Skill == h.Skill && h.At - runs[^1].End < SameCast)
                runs[^1] = (runs[^1].At, h.At, h.Skill, runs[^1].Hits + 1);
            else
                runs.Add((h.At, h.At, h.Skill, 1));
        }
        return runs;
    }

    private static readonly double[] Steps = [1, 2, 3, 5, 10, 15, 30, 60, 120, 300, 600, 900, 1800, 3600];

    /// <summary>The time axis' label step in seconds: the smallest round one that gives at most eight labels.</summary>
    public static double AxisStep(double seconds) => Steps.FirstOrDefault(s => seconds / s <= 7, Steps[^1]);

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

    /// <summary>The share of the casts made while one of the spans was on, 0–1 ("Баффы по умениям").</summary>
    public static double ActiveShare(IReadOnlyCollection<DateTime> casts, IReadOnlyList<(DateTime From, DateTime To)> spans) =>
        casts.Count == 0 ? 0 : casts.Count(c => spans.Any(s => c >= s.From && c < s.To)) / (double)casts.Count;

    /// <summary>The share of the fight the merged intervals cover, 0–1.</summary>
    public static double Uptime(IEnumerable<(DateTime From, DateTime To)> merged, DateTime start, DateTime end) =>
        end > start ? merged.Sum(i => (i.To - i.From).TotalSeconds) / (end - start).TotalSeconds : 0;
}
