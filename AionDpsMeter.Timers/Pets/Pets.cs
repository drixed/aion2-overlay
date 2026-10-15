namespace AionDpsMeter.Timers.Pets;

public sealed record PetSoul(int Pet, int Amount);

/// <param name="Souls">Souls within the level from the login list; null for pets it does not list (maxed ones).</param>
public sealed record PetLevel(int Pet, int Level, int? Souls = null);

/// <param name="Souls">Souls within the current level; null until the game has given an exact count.</param>
/// <param name="Need">Souls the current level needs: 5 to collect, 25 to level 2, 75 to level 3.</param>
/// <param name="SessionGain">Souls gained since the meter started.</param>
public sealed record PetView(int Pet, int Level, int? Souls, int Need, int SessionGain);

/// <param name="Monster">The monster its souls come from (learned from kills), or null while unknown.</param>
public sealed record PetRow(PetView View, int? Monster);

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

    /// <summary><c>u32, u16 count, then (u32 pet, u32 pet, u32 level)</c> records while the two ids match, then a varint
    /// count of <c>(u32 pet, u32 souls within the level)</c> pairs.</summary>
    public static IReadOnlyList<PetLevel> ParseList(ReadOnlySpan<byte> body)
    {
        var pets = new List<PetLevel>();
        var o = 6;
        for (; o + 12 <= body.Length; o += 12)
        {
            var id = BitConverter.ToInt32(body[o..(o + 4)]);
            if (id != BitConverter.ToInt32(body[(o + 4)..(o + 8)]) || id is < 1 or > 100_000) break;
            var level = BitConverter.ToInt32(body[(o + 8)..(o + 12)]);
            if (level is < 0 or > 3) break;
            pets.Add(new PetLevel(id, level));
        }
        var souls = ParseSouls(body[o..]);
        return pets.Select(p => souls.TryGetValue(p.Pet, out var s) ? p with { Souls = s } : p).ToList();
    }

    private static Dictionary<int, int> ParseSouls(ReadOnlySpan<byte> rest)
    {
        var souls = new Dictionary<int, int>();
        int count = 0, shift = 0, o = 0;
        for (; o < rest.Length && o < 3; o++)
        {
            count |= (rest[o] & 0x7F) << shift;
            shift += 7;
            if ((rest[o] & 0x80) == 0) break;
        }
        o++;
        if (count <= 0 || o + count * 8 > rest.Length) return souls; // not the pairs this build knows: keep the levels only
        for (var i = 0; i < count; i++, o += 8)
        {
            var id = BitConverter.ToInt32(rest[o..(o + 4)]);
            var value = BitConverter.ToInt32(rest[(o + 4)..(o + 8)]);
            if (id is < 1 or > 100_000 || value is < 0 or >= 10_000) return [];
            souls[id] = value;
        }
        return souls;
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
                if (l.Souls is { } souls) e.Souls = souls; // the game's own count beats ours
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

    /// <summary>The level-up packet names no pet: it is the one whose level is full. Without one the packet is ignored
    /// (it also comes for other things); a level gained unseen shows up in the login list.</summary>
    public int? OnLevelUp(int newLevel)
    {
        lock (gate)
        {
            var pick = pets.Values
                .Where(e => e.Level == newLevel - 1 && e.Souls >= Need(e.Level))
                .OrderByDescending(e => lastSoul.GetValueOrDefault(e.Pet))
                .FirstOrDefault();
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

    /// <summary>Every pet: closest to its next level first, then unknown counts, maxed ones last.</summary>
    public IReadOnlyList<PetRow> All()
    {
        lock (gate)
        {
            return pets.Values
                .Select(e => new PetRow(
                    new PetView(e.Pet, e.Level, e.Souls, Need(e.Level), session.GetValueOrDefault(e.Pet)),
                    e.Monsters.Count > 0 ? e.Monsters.MaxBy(m => m.Value).Key : null))
                .OrderBy(r => r.View.Level >= MaxLevel ? 2 : r.View.Souls is null ? 1 : 0)
                .ThenBy(r => r.View.Souls is { } s ? r.View.Need - s : int.MaxValue)
                .ThenBy(r => r.View.Pet)
                .ToList();
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
