namespace AionDpsMeter.Timers.Modes;

/// <summary>What the main window's rows show; picked from the menu in its header.</summary>
public enum MeterMode { Dps, Damage, Heal, Dummy, Grind }

/// <param name="OnDummy">The target is a training scarecrow.</param>
public sealed record Hit(long PlayerId, long Damage, bool OnDummy, DateTime At);

public sealed record Heal(long CasterId, long TargetId, long Amount, DateTime At);

public sealed record ModeRow(long PlayerId, long Total, double PerSecond);

public sealed record DummyView(IReadOnlyList<ModeRow> Rows, TimeSpan Elapsed, TimeSpan Left, bool Finished);

public sealed record GrindView(IReadOnlyList<ModeRow> Rows, TimeSpan Elapsed, bool Running);

internal static class Rows
{
    public static IReadOnlyList<ModeRow> Of(Dictionary<long, long> totals, TimeSpan over) =>
        totals.Select(kv => new ModeRow(kv.Key, kv.Value, over.TotalSeconds > 0 ? kv.Value / over.TotalSeconds : 0))
            .OrderByDescending(r => r.Total)
            .ToList();
}

/// <summary>
/// A fixed-length run on a training scarecrow: it starts with the first hit on one and counts exactly the run length,
/// so runs compare fairly. A pause after a run (5 s without counting) lets the next hit start a new one.
/// </summary>
public sealed class DummyRun(TimeSpan length)
{
    private static readonly TimeSpan Pause = TimeSpan.FromSeconds(5);
    private readonly Lock gate = new();
    private readonly Dictionary<long, long> totals = [];
    private DateTime? start;

    public TimeSpan Length { get; } = length;

    public void Add(Hit hit)
    {
        if (!hit.OnDummy) return;
        lock (gate)
        {
            if (start is null || hit.At >= start.Value + Length + Pause)
            {
                start = hit.At;
                totals.Clear();
            }
            if (hit.At >= start.Value + Length) return; // the run is over
            totals[hit.PlayerId] = totals.GetValueOrDefault(hit.PlayerId) + hit.Damage;
        }
    }

    public DummyView View(DateTime now)
    {
        lock (gate)
        {
            if (start is not { } s) return new DummyView([], TimeSpan.Zero, Length, false);
            var elapsed = now - s < Length ? now - s : Length;
            if (elapsed < TimeSpan.Zero) elapsed = TimeSpan.Zero;
            return new DummyView(Rows.Of(totals, elapsed), elapsed, Length - elapsed, elapsed >= Length);
        }
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
