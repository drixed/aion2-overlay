namespace AionDpsMeter.Timers.Modes;

/// <summary>What the main window's rows show; picked from the menu in its header.</summary>
public enum MeterMode { Dps, Damage, Heal, Dummy, Grind, Pvp }

/// <param name="OnDummy">The target is a training scarecrow.</param>
/// <param name="Mine">The user's own hit.</param>
public sealed record Hit(long PlayerId, long Damage, bool OnDummy, DateTime At, bool Mine = false);

public sealed record Heal(long CasterId, long TargetId, long Amount, DateTime At);

public sealed record ModeRow(long PlayerId, long Total, double PerSecond);

/// <param name="Started">The user has a run; Elapsed, Left and Finished are about it.</param>
public sealed record DummyView(IReadOnlyList<ModeRow> Rows, TimeSpan Elapsed, TimeSpan Left, bool Finished, bool Started);

public sealed record GrindView(IReadOnlyList<ModeRow> Rows, TimeSpan Elapsed, bool Running);

internal static class Rows
{
    public static IReadOnlyList<ModeRow> Of(Dictionary<long, long> totals, TimeSpan over) =>
        totals.Select(kv => new ModeRow(kv.Key, kv.Value, over.TotalSeconds > 0 ? kv.Value / over.TotalSeconds : 0))
            .OrderByDescending(r => r.Total)
            .ToList();
}

/// <summary>
/// Fixed-length runs on training scarecrows, one per player: each starts with that player's first hit on one and counts
/// exactly the run length, so runs compare fairly. Hitting on after the end keeps the result; the first hit 5 s after
/// both the end and the last hit starts a new run. The time left is the user's own run.
/// </summary>
public sealed class DummyRun(TimeSpan length)
{
    private static readonly TimeSpan Pause = TimeSpan.FromSeconds(5);
    private readonly Lock gate = new();
    private readonly Dictionary<long, Run> runs = [];
    private long? me;

    private sealed class Run
    {
        public DateTime Start;
        public DateTime LastHit;
        public long Total;
    }

    public TimeSpan Length { get; } = length;

    public void Add(Hit hit)
    {
        if (!hit.OnDummy) return;
        lock (gate)
        {
            if (hit.Mine) me = hit.PlayerId;
            if (!runs.TryGetValue(hit.PlayerId, out var run))
                runs[hit.PlayerId] = run = new Run { Start = hit.At, LastHit = hit.At };
            else if (hit.At >= run.Start + Length && hit.At - Max(run.LastHit, run.Start + Length) >= Pause)
            {
                run.Start = hit.At;
                run.Total = 0;
            }
            run.LastHit = hit.At;
            if (hit.At < run.Start + Length) run.Total += hit.Damage; // after the run: not counted
        }
    }

    /// <summary>The user's run and everyone who hit a scarecrow within the run length; players without the user's
    /// run started still show, with <see cref="DummyView.Started"/> false.</summary>
    public DummyView View(DateTime now)
    {
        lock (gate)
        {
            var rows = runs
                .Where(kv => kv.Key == me || now - kv.Value.LastHit <= Length)
                .Select(kv =>
                {
                    var elapsed = Elapsed(kv.Value, now);
                    return new ModeRow(kv.Key, kv.Value.Total, elapsed.TotalSeconds > 0 ? kv.Value.Total / elapsed.TotalSeconds : 0);
                })
                .OrderByDescending(r => r.Total)
                .ToList();
            if (me is not { } id || !runs.TryGetValue(id, out var mine)) return new DummyView(rows, TimeSpan.Zero, Length, false, false);
            var mineElapsed = Elapsed(mine, now);
            return new DummyView(rows, mineElapsed, Length - mineElapsed, mineElapsed >= Length, true);
        }
    }

    private static DateTime Max(DateTime a, DateTime b) => a > b ? a : b;

    private TimeSpan Elapsed(Run run, DateTime now)
    {
        var elapsed = now - run.Start;
        return elapsed < TimeSpan.Zero ? TimeSpan.Zero : elapsed > Length ? Length : elapsed;
    }
}

/// <summary>Grinding: every hit on any target between Play and Stop, with DPS over the whole time.</summary>
public sealed class GrindSession
{
    private readonly Lock gate = new();
    private readonly Dictionary<long, long> totals = [];
    private DateTime? start;
    private DateTime? stop;

    public bool Running { get { lock (gate) return start is not null && stop is null; } }

    public void Play(DateTime now)
    {
        lock (gate)
        {
            totals.Clear();
            start = now;
            stop = null;
        }
    }

    public void Stop(DateTime now)
    {
        lock (gate) { if (start is not null && stop is null) stop = now; }
    }

    public void Add(Hit hit)
    {
        lock (gate)
        {
            if (start is not { } s || stop is not null || hit.At < s) return;
            totals[hit.PlayerId] = totals.GetValueOrDefault(hit.PlayerId) + hit.Damage;
        }
    }

    public GrindView View(DateTime now)
    {
        lock (gate)
        {
            if (start is not { } s) return new GrindView([], TimeSpan.Zero, false);
            var elapsed = (stop ?? now) - s;
            return new GrindView(Rows.Of(totals, elapsed), elapsed, stop is null);
        }
    }
}

/// <summary>Healing on other players per caster (self-heals do not show how much a healer carried).</summary>
public sealed class HealTally
{
    private static readonly TimeSpan Keep = TimeSpan.FromMinutes(30);
    private readonly Lock gate = new();
    private readonly List<Heal> heals = [];

    public void Add(Heal heal)
    {
        if (heal.CasterId == heal.TargetId || heal.Amount <= 0) return;
        lock (gate)
        {
            heals.Add(heal);
            if (heals.Count > 4096) heals.RemoveAll(h => h.At < heal.At - Keep);
        }
    }

    public IReadOnlyList<ModeRow> Rows(DateTime from, DateTime to)
    {
        lock (gate)
        {
            var totals = new Dictionary<long, long>();
            foreach (var h in heals.Where(h => h.At >= from && h.At <= to))
                totals[h.CasterId] = totals.GetValueOrDefault(h.CasterId) + h.Amount;
            return Modes.Rows.Of(totals, to - from);
        }
    }
}
