namespace AionDpsMeter.Timers.Pets;

public sealed record PetSoul(int Pet, int Amount);

public sealed record PetLevel(int Pet, int Level);

/// <param name="Souls">Souls within the current level; null until the game has given an exact count.</param>
/// <param name="Need">Souls the current level needs: 5 to collect, 25 to level 2, 75 to level 3.</param>
/// <param name="SessionGain">Souls gained since the meter started.</param>
public sealed record PetView(int Pet, int Level, int? Souls, int Need, int SessionGain);

/// <summary>
/// Pet packets (EU capture 2026-10-16), bodies after the opcode. Wire bytes → RATmeter opcode (little-endian):
/// <c>00 90</c> login list, <c>0D 90</c> souls gained, <c>0E 90</c> exact souls when a level is full, <c>5C 8D</c> level up.
/// </summary>
public static class PetParser
{
    public const ushort ListOpcode = 0x9000, SoulOpcode = 0x900D, ExactOpcode = 0x900E, LevelUpOpcode = 0x8D5C;

    /// <summary><c>01, u32 pet, u32 amount</c>.</summary>
    public static PetSoul? ParseSoul(ReadOnlySpan<byte> body) => ReadPair(body);

    /// <summary><c>01, u32 pet, u32 souls</c> — the count within the level, sent when it is full.</summary>
    public static PetSoul? ParseExact(ReadOnlySpan<byte> body) => ReadPair(body);

    /// <summary><c>01, u32 collection id (not the pet id), u32 new level, …</c>.</summary>
    public static int? ParseLevelUp(ReadOnlySpan<byte> body)
    {
        if (body.Length < 9 || body[0] != 0x01) return null;
        var level = BitConverter.ToInt32(body[5..9]);
        return level is >= 1 and <= 3 ? level : null;
    }

    /// <summary><c>u32, u16 count, then (u32 pet, u32 pet, u32 level)</c> records while the two ids match.</summary>
    public static IReadOnlyList<PetLevel> ParseList(ReadOnlySpan<byte> body)
    {
        var pets = new List<PetLevel>();
        for (var o = 6; o + 12 <= body.Length; o += 12)
        {
            var id = BitConverter.ToInt32(body[o..(o + 4)]);
            if (id != BitConverter.ToInt32(body[(o + 4)..(o + 8)]) || id is < 1 or > 100_000) break;
            var level = BitConverter.ToInt32(body[(o + 8)..(o + 12)]);
            if (level is < 0 or > 3) break;
            pets.Add(new PetLevel(id, level));
        }
        return pets;
    }

    private static PetSoul? ReadPair(ReadOnlySpan<byte> body)
    {
        if (body.Length < 9 || body[0] != 0x01) return null;
        var pet = BitConverter.ToInt32(body[1..5]);
        var value = BitConverter.ToInt32(body[5..9]);
        return pet is > 0 and < 100_000 && value is >= 0 and < 10_000 ? new PetSoul(pet, value) : null;
    }
}

/// <summary>
/// Each pet's level and souls, kept from the game's packets: the level comes at every login, "+souls" after kills,
/// the exact count when a level is full. Which monster gives which pet is learned from the kills just before a soul.
/// </summary>
public sealed class PetBook
{
    public const int MaxLevel = 3;

    public static int Need(int level) => level switch { 0 => 5, 1 => 25, 2 => 75, _ => 0 };

    public sealed class Entry
    {
        public int Pet { get; set; }
        public int Level { get; set; }
        public int? Souls { get; set; }
        /// <summary>Monster code → souls seen right after killing it.</summary>
        public Dictionary<int, int> Monsters { get; set; } = [];
    }

    private readonly Lock gate = new();
    private readonly Dictionary<int, Entry> pets = [];
    private readonly Dictionary<int, int> session = [];
    private readonly Dictionary<int, long> lastSoul = [];
    private long tick;

    public void OnList(IEnumerable<PetLevel> levels)
    {
        lock (gate)
        {
            foreach (var l in levels)
            {
                var e = Get(l.Pet);
                if (e.Level != l.Level) e.Souls = l.Level > e.Level ? null : e.Souls; // a level gained while we were away
                e.Level = l.Level;
            }
        }
    }

    public void OnSoul(int pet, int amount, int? monsterCode)
    {
        lock (gate)
        {
            var e = Get(pet);
            if (e.Souls is { } s) e.Souls = s + amount;
            session[pet] = session.GetValueOrDefault(pet) + amount;
            lastSoul[pet] = ++tick;
            if (monsterCode is int code && code > 0) e.Monsters[code] = e.Monsters.GetValueOrDefault(code) + amount;
        }
    }

    public void OnExact(int pet, int souls)
    {
        lock (gate) { Get(pet).Souls = souls; }
    }

    /// <summary>The level-up packet names no pet: it is the one whose level is full, else the last to get a soul.</summary>
    public int? OnLevelUp(int newLevel)
    {
        lock (gate)
        {
            var candidates = pets.Values.Where(e => e.Level == newLevel - 1).ToList();
            var pick = candidates.Where(e => e.Souls >= Need(e.Level)).OrderByDescending(e => lastSoul.GetValueOrDefault(e.Pet)).FirstOrDefault()
                ?? candidates.OrderByDescending(e => lastSoul.GetValueOrDefault(e.Pet)).FirstOrDefault(e => lastSoul.ContainsKey(e.Pet));
            if (pick is null) return null;
            pick.Souls = pick.Souls is { } s ? Math.Max(0, s - Need(pick.Level)) : null; // the extra souls carry over
            pick.Level = newLevel;
            return pick.Pet;
        }
    }

    /// <summary>The pet this monster gives souls to, while it is not maxed; null otherwise.</summary>
    public PetView? ForMonster(int monsterCode)
    {
        lock (gate)
        {
            var e = pets.Values
                .Where(p => p.Monsters.TryGetValue(monsterCode, out var n) && n == p.Monsters.Values.Max())
                .OrderByDescending(p => p.Monsters[monsterCode])
                .FirstOrDefault();
            if (e is null || e.Level >= MaxLevel) return null;
            return new PetView(e.Pet, e.Level, e.Souls, Need(e.Level), session.GetValueOrDefault(e.Pet));
        }
    }

    public List<Entry> Snapshot()
    {
        lock (gate)
            return pets.Values.Select(e => new Entry { Pet = e.Pet, Level = e.Level, Souls = e.Souls, Monsters = new(e.Monsters) }).ToList();
    }

    public static PetBook FromSnapshot(IEnumerable<Entry> entries)
    {
        var book = new PetBook();
        foreach (var e in entries) book.pets[e.Pet] = e;
        return book;
    }

    private Entry Get(int pet)
    {
        if (!pets.TryGetValue(pet, out var e)) pets[pet] = e = new Entry { Pet = pet };
        return e;
    }
}
