// aion2-overlay fork: new file (see FORK_CHANGES.md).
namespace AionDpsMeter.Timers.Buffs;

/// <summary>A class's set of buffs to watch ("Пресеты баффов"); one preset per class is the active one.</summary>
public sealed class BuffPreset
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string Name { get; set; } = "Пресет";
    public int ClassId { get; set; }
    public List<int> BuffIds { get; set; } = [];
}

public sealed record SeenBuff(int Id, string Name, string? Icon);

/// <param name="Left">Time left, or null when the buff is not on the player.</param>
/// <param name="Share">The part of its duration still left, 0–1.</param>
public sealed record BuffTile(int BuffId, string Name, string? Icon, TimeSpan? Left, double Share);

/// <summary>
/// The player's own buffs as the game sends them (each cast with its duration): what is on now and for how long, which
/// tracked buff just ran out (once), and which buffs were seen at all (the preset picker). Thread-safe: buffs come from
/// the packet thread, reads from the UI.
/// </summary>
public sealed class BuffTracker
{
    private const int SeenMax = 200;
    private readonly Lock gate = new();
    private readonly Dictionary<int, (string Name, string? Icon, DateTimeOffset From, DateTimeOffset Until)> on = [];
    private readonly List<SeenBuff> seen = [];
    private readonly HashSet<int> reported = [];

    public void OnBuff(int buffId, string name, string? icon, uint durationMs, DateTimeOffset at)
    {
        lock (gate)
        {
            on[buffId] = (name, icon, at, at.AddMilliseconds(durationMs));
            reported.Remove(buffId);
            seen.RemoveAll(s => s.Id == buffId);
            seen.Insert(0, new SeenBuff(buffId, name, icon));
            if (seen.Count > SeenMax) seen.RemoveRange(SeenMax, seen.Count - SeenMax);
        }
    }

    public IReadOnlyList<BuffTile> Tiles(BuffPreset preset, DateTimeOffset now)
    {
        lock (gate)
        {
            return preset.BuffIds.Select(id =>
            {
                var known = seen.FirstOrDefault(s => s.Id == id);
                if (on.TryGetValue(id, out var b) && b.Until > now)
                {
                    var total = Math.Max(1, (b.Until - b.From).TotalMilliseconds);
                    return new BuffTile(id, b.Name, b.Icon, b.Until - now, (b.Until - now).TotalMilliseconds / total);
                }
                return new BuffTile(id, known?.Name ?? $"Бафф {id}", known?.Icon, null, 0);
            }).ToList();
        }
    }

    /// <summary>Tracked buffs that were on and have run out since the last call; each run-out is reported once.</summary>
    public IReadOnlyList<SeenBuff> Expired(BuffPreset preset, DateTimeOffset now)
    {
        lock (gate)
        {
            var gone = new List<SeenBuff>();
            foreach (var id in preset.BuffIds)
            {
                if (!on.TryGetValue(id, out var b) || b.Until > now || !reported.Add(id)) continue;
                gone.Add(new SeenBuff(id, b.Name, b.Icon));
            }
            return gone;
        }
    }

    public IReadOnlyList<SeenBuff> Seen()
    {
        lock (gate) return seen.ToList();
    }

    public void Remember(IEnumerable<SeenBuff> saved)
    {
        lock (gate)
            foreach (var s in saved)
                if (seen.All(x => x.Id != s.Id) && seen.Count < SeenMax) seen.Add(s);
    }

    /// <summary>"Показывать трекеры в": "bosses" — fights with a boss or a dummy, "fights" — every fight, "always".</summary>
    public static bool Shows(string mode, bool inFight, bool bossOrDummy) => mode switch
    {
        "always" => true,
        "fights" => inFight,
        _ => inFight && bossOrDummy,
    };
}
