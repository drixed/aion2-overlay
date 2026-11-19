using AionDpsMeter.Timers.Schedule;

namespace AionDpsMeter.Timers.Bosses;

/// <param name="Key">The NPC code, or -<paramref name="SlotId"/> while the list slot's boss is not known yet.</param>
public sealed record BossTimer(
    int ServerId,
    int Key,
    int MapId,
    int SlotId,
    bool Alive,
    DateTimeOffset? AliveSince,
    DateTimeOffset? NextSpawn,
    DateTimeOffset? LastKill,
    DateTimeOffset? ListedAt);

/// <param name="LearnedSlots">List place (map × 100 + place) → NPC code, for maps whose bosses are not one block.</param>
public sealed record TrackerState(
    List<BossTimer> Timers,
    Dictionary<int, int> LearnedBlocks,
    Dictionary<int, int> LearnedRespawnMinutes,
    Dictionary<int, int>? LearnedSlots = null);

/// <summary>
/// Field boss respawn timers, per server. Sources, newest wins: the in-game map list (the server's own times),
/// a boss seen dying (kill + respawn interval), the user's "killed" button. Thread-safe: lists arrive on the packet
/// thread, kills on the watcher, button presses and reads on the UI thread.
/// </summary>
public sealed class FieldBossTracker(BossCatalog catalog, Func<ScheduleData> data, TimeProvider time)
{
    private static readonly TimeSpan SameKill = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan ListMemory = TimeSpan.FromMinutes(10);

    private readonly object gate = new();
    private readonly Dictionary<(int Server, int Key), BossTimer> timers = new();
    private readonly Dictionary<int, int> learnedBlocks = new();   // map → NPC code block
    private readonly Dictionary<int, int> respawnMinutes = new();  // NPC code → learned interval
    private readonly Dictionary<(int Server, int Map), (FieldBossList List, DateTimeOffset At)> lastLists = new();
    private readonly Dictionary<int, int> learnedSlots = new();     // list place → NPC code
    private readonly Dictionary<int, List<(int Code, DateTimeOffset At)>> unplacedKills = new(); // server → kills not yet placed

    public event Action? Changed;

    public IReadOnlyList<BossTimer> TimersFor(int serverId)
    {
        lock (gate) return timers.Values.Where(t => t.ServerId == serverId).ToList();
    }

    public int RespawnMinutesOf(int code)
    {
        lock (gate) return RespawnOf(code);
    }

    /// <summary>A boss of a field map (known from schedule.json or learned): it respawns and has no enrage timer.</summary>
    public bool IsFieldBoss(int code)
    {
        lock (gate) return IsFieldBlock(code / 1000) || learnedSlots.ContainsValue(code) || data().FieldBossSlots?.Values.Contains(code) == true;
    }

    public void OnList(int serverId, FieldBossList list)
    {
        lock (gate)
        {
            // Sent about once a second while the map is open: an unchanged list changes nothing.
            if (lastLists.TryGetValue((serverId, list.MapId), out var previous) && previous.List.SameAs(list)) return;
            var now = time.GetUtcNow();
            lastLists[(serverId, list.MapId)] = (list, now);
            Apply(serverId, list, now);
        }
        Changed?.Invoke();
    }

    /// <summary>A boss died in view. Counted only for field bosses: its block belongs to a field map, it is already
    /// tracked, or a recent list of an unnamed map matches its block.</summary>
    public void OnKill(int serverId, int code)
    {
        if (!catalog.IsBoss(code)) return;
        lock (gate)
        {
            var now = time.GetUtcNow();
            if (!timers.ContainsKey((serverId, code)) && !IsFieldBlock(code / 1000) && !TryLearnBlock(serverId, code, now))
            {
                // Maybe a boss of a map whose places are not one block: the next list shows which place went down.
                var kills = unplacedKills.TryGetValue(serverId, out var k) ? k : unplacedKills[serverId] = [];
                kills.RemoveAll(x => now - x.At > ListMemory || x.Code == code);
                kills.Add((code, now));
                return;
            }
            if (!RecordKill(serverId, code, now, manual: false)) return;
        }
        Changed?.Invoke();
    }

    /// <summary>The user says a tracked boss is dead (the overlay's "убит" button).</summary>
    public void MarkKilled(int serverId, int key)
    {
        lock (gate)
        {
            if (!timers.ContainsKey((serverId, key))) return;
            RecordKill(serverId, key, time.GetUtcNow(), manual: true);
        }
        Changed?.Invoke();
    }

    /// <summary>The user names a list place ("кто здесь?"); 0 forgets the name. Its timers move under the boss.</summary>
    public void AssignSlot(int slotId, int code)
    {
        lock (gate)
        {
            if (code > 0) learnedSlots[slotId] = code;
            else learnedSlots.Remove(slotId);
            foreach (var t in timers.Values.Where(t => t.SlotId == slotId && t.Key != (code > 0 ? code : -slotId)).ToList())
            {
                timers.Remove((t.ServerId, t.Key));
                timers[(t.ServerId, code > 0 ? code : -slotId)] = t with { Key = code > 0 ? code : -slotId };
            }
        }
        Changed?.Invoke();
    }

    public TrackerState Export()
    {
        lock (gate) return new TrackerState(timers.Values.ToList(), new(learnedBlocks), new(respawnMinutes), new(learnedSlots));
    }

    public void Import(TrackerState? state)
    {
        if (state is null) return;
        lock (gate)
        {
            foreach (var t in state.Timers ?? []) if (t is not null) timers[(t.ServerId, t.Key)] = t; // hand-edited files
            foreach (var (map, block) in state.LearnedBlocks ?? new()) learnedBlocks[map] = block;
            foreach (var (code, minutes) in state.LearnedRespawnMinutes ?? new()) respawnMinutes[code] = minutes;
            foreach (var (slot, code) in state.LearnedSlots ?? new()) learnedSlots[slot] = code;
        }
        Changed?.Invoke();
    }

    private void Apply(int serverId, FieldBossList list, DateTimeOffset now)
    {
        var block = BlockOf(list.MapId);
        if (block == 0) LearnSlot(serverId, list, now);
        foreach (var slot in list.Slots)
        {
            var code = block != 0 ? catalog.BossInSlot(block, list.MapId, slot.SlotId, list.Count) : SlotCode(slot.SlotId);
            var key = code != 0 ? code : -slot.SlotId;
            if (code != 0 && timers.Remove((serverId, -slot.SlotId), out var unnamed) && !timers.ContainsKey((serverId, key)))
                timers[(serverId, key)] = unnamed with { Key = key }; // listed before its boss was known
            var old = timers.GetValueOrDefault((serverId, key));
            DateTimeOffset? at = slot.AtMs > 0 ? DateTimeOffset.FromUnixTimeMilliseconds(slot.AtMs) : null;
            if (code != 0 && !slot.Alive && at is { } back) LearnRespawn(code, old, back, now);
            timers[(serverId, key)] = new BossTimer(serverId, key, list.MapId, slot.SlotId, slot.Alive,
                AliveSince: slot.Alive ? at : null,
                NextSpawn: slot.Alive ? null : at,
                LastKill: old?.LastKill,
                ListedAt: now);
        }
    }

    /// <summary>A boss seen dying and exactly one unnamed place of this map gone from alive to down since: that place is
    /// the boss's. Two places going down together, or two kills waiting, teach nothing.</summary>
    private void LearnSlot(int serverId, FieldBossList list, DateTimeOffset now)
    {
        if (!unplacedKills.TryGetValue(serverId, out var kills)) return;
        kills.RemoveAll(x => now - x.At > ListMemory);
        if (kills.Count != 1) return;
        var downed = list.Slots
            .Where(s => !s.Alive && SlotCode(s.SlotId) == 0
                        && timers.TryGetValue((serverId, -s.SlotId), out var old) && old.Alive)
            .ToList();
        if (downed.Count != 1) return;
        var (code, at) = kills[0];
        if (learnedSlots.ContainsValue(code)) { kills.Clear(); return; }
        learnedSlots[downed[0].SlotId] = code;
        kills.Clear();
        if (timers.Remove((serverId, -downed[0].SlotId), out var unnamed))
            timers[(serverId, code)] = unnamed with { Key = code, LastKill = at };
    }

    /// <summary>A boss that just went down shows its comeback time: with a fresh death that gives the interval.</summary>
    private void LearnRespawn(int code, BossTimer? old, DateTimeOffset back, DateTimeOffset now)
    {
        DateTimeOffset? died = null;
        if (old?.LastKill is { } kill && kill <= now && now - kill < TimeSpan.FromMinutes(30) && back > kill) died = kill;
        else if (old is { Alive: true, ListedAt: { } listed } && now - listed < TimeSpan.FromMinutes(3)) died = now;
        if (died is not { } d) return;
        var minutes = (int)Math.Round((back - d).TotalMinutes / 5.0) * 5;
        if (minutes is >= 5 and <= 48 * 60) respawnMinutes[code] = minutes;
    }

    private bool RecordKill(int serverId, int key, DateTimeOffset now, bool manual)
    {
        var old = timers.GetValueOrDefault((serverId, key));
        if (!manual && old?.LastKill is { } last && now - last < SameKill) return false;
        var minutes = key > 0 ? RespawnOf(key) : 0;
        timers[(serverId, key)] = new BossTimer(serverId, key, old?.MapId ?? 0, old?.SlotId ?? 0, Alive: false,
            AliveSince: null,
            NextSpawn: minutes > 0 ? now.AddMinutes(minutes) : null,
            LastKill: now,
            ListedAt: old?.ListedAt);
        return true;
    }

    /// <summary>A boss from an unnamed block died: if exactly one recent list of a map with no known block has as many
    /// slots as that block has bosses, that map is the block's.</summary>
    private bool TryLearnBlock(int serverId, int code, DateTimeOffset now)
    {
        var block = code / 1000;
        var size = catalog.FieldBossesInBlock(block).Count;
        if (size == 0) return false;
        var candidates = lastLists
            .Where(kv => kv.Key.Server == serverId && now - kv.Value.At <= ListMemory
                         && BlockOf(kv.Key.Map) == 0 && kv.Value.List.Count == size)
            .ToList();
        if (candidates.Count != 1) return false;
        var match = candidates[0];
        learnedBlocks[match.Key.Map] = block;
        Apply(serverId, match.Value.List, match.Value.At);
        return true;
    }

    private bool IsFieldBlock(int block) =>
        data().FieldBossMaps.Values.Any(m => m.Block == block) || learnedBlocks.ContainsValue(block);

    private int BlockOf(int mapId) =>
        data().FieldBossMaps.TryGetValue(mapId, out var map) ? map.Block : learnedBlocks.GetValueOrDefault(mapId);

    /// <summary>A place's boss: named by the user or learned from a kill first, then schedule.json; 0 when unknown.</summary>
    private int SlotCode(int slotId) =>
        learnedSlots.TryGetValue(slotId, out var code) ? code : data().FieldBossSlots?.GetValueOrDefault(slotId) ?? 0;

    private int RespawnOf(int code) =>
        respawnMinutes.TryGetValue(code, out var learned) ? learned : data().RespawnMinutes.GetValueOrDefault(code);
}
