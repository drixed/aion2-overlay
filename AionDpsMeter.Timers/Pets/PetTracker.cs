using System.Text.Json;
using AionDpsMeter.Core.Models;
using AionDpsMeter.Services.PacketProcessing.Fork;
using AionDpsMeter.Services.PacketProcessing.Routing;
using AionDpsMeter.Timers.Bosses;
using Microsoft.Extensions.Logging;

namespace AionDpsMeter.Timers.Pets;

public sealed record PetLevelNews(int Pet, int Level, string Name, DateTimeOffset At);

/// <summary>
/// Pets from the game's packets, kept in pets-state.json across restarts. The monster that gave a soul is the one the
/// player killed just before it (deaths of other players' monsters nearby are ignored).
/// </summary>
public sealed class PetTracker : IPetListener, IDisposable
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    private static readonly TimeSpan HitMemory = TimeSpan.FromSeconds(30), DeathMemory = TimeSpan.FromSeconds(5), ShowFor = TimeSpan.FromSeconds(60);

    private readonly string path;
    private readonly BossCatalog catalog;
    private readonly TimeProvider time;
    private readonly ILogger<PetTracker> logger;
    private readonly PetBook book;
    private readonly Lock gate = new();
    private readonly Dictionary<int, DateTime> myTargets = [];       // entity id → my last hit on it
    private readonly List<(int Code, DateTime At)> myKills = [];      // monsters I hit that died
    private int lastHitCode;
    private DateTime lastHitAt;

    public event Action<PetLevelNews>? LevelUp;

    public PetLevelNews? Latest { get; private set; }

    public PetTracker(string path, BossCatalog catalog, TimeProvider time, ILogger<PetTracker> logger)
    {
        this.path = path;
        this.catalog = catalog;
        this.time = time;
        this.logger = logger;
        book = Load();
        CombatFeed.Damage += OnDamage;
        CombatFeed.MobDeath += OnMobDeath;
    }

    /// <summary>The pet of the monster I hit last (within a minute), unless it is maxed or unknown.</summary>
    public (PetView Pet, string Name)? Current()
    {
        int code;
        lock (gate)
        {
            if (lastHitCode == 0 || DateTime.Now - lastHitAt > ShowFor) return null;
            code = lastHitCode;
        }
        return book.ForMonster(code) is { } view ? (view, NameOf(code)) : null;
    }

    public void OnPetPacket(ushort opcode, Packet packet)
    {
        try
        {
            if (!Wire.TryVarint(packet.Data, 0, out _, out var header) || packet.Data.Length < header + 2) return;
            var body = packet.Data.AsSpan(header + 2);
            switch (opcode)
            {
                case PetParser.ListOpcode:
                    book.OnList(PetParser.ParseList(body));
                    break;
                case PetParser.SoulOpcode when PetParser.ParseSoul(body) is { } soul:
                    book.OnSoul(soul.Pet, soul.Amount, LastKill());
                    break;
                case PetParser.ExactOpcode when PetParser.ParseExact(body) is { } exact:
                    book.OnExact(exact.Pet, exact.Amount);
                    break;
                case PetParser.LevelUpOpcode when PetParser.ParseLevelUp(body) is { } level:
                    if (book.OnLevelUp(level) is { } pet)
                    {
                        var news = new PetLevelNews(pet, level, NameOfPet(pet), time.GetUtcNow());
                        Latest = news;
                        LevelUp?.Invoke(news);
                    }
                    break;
                default:
                    return;
            }
            Save();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Pet packet 0x{Opcode:X4} skipped", opcode);
        }
    }

    private void OnDamage(PlayerDamage d)
    {
        if (d.SourceEntity is not { IsUser: true } || d.TargetEntity is not { } target) return;
        lock (gate)
        {
            myTargets[target.Id] = d.DateTime;
            if (target.MobCode > 0)
            {
                lastHitCode = target.MobCode;
                lastHitAt = DateTime.Now;
            }
            if (myTargets.Count > 256)
                foreach (var old in myTargets.Where(t => d.DateTime - t.Value > HitMemory).Select(t => t.Key).ToList()) myTargets.Remove(old);
        }
    }

    private void OnMobDeath(int entityId, int code, DateTime at)
    {
        if (code <= 0) return;
        lock (gate)
        {
            if (!myTargets.TryGetValue(entityId, out var hit) || at - hit > HitMemory) return;
            myKills.Add((code, at));
            myKills.RemoveAll(k => at - k.At > DeathMemory);
        }
    }

    private int? LastKill()
    {
        lock (gate)
        {
            var now = DateTime.Now;
            return myKills.Where(k => now - k.At <= DeathMemory).OrderByDescending(k => k.At).Select(k => (int?)k.Code).FirstOrDefault();
        }
    }

    private string NameOf(int monsterCode) => catalog.NameOrNull(monsterCode) ?? $"Монстр {monsterCode}";

    private string NameOfPet(int pet) =>
        book.Snapshot().FirstOrDefault(e => e.Pet == pet)?.Monsters.OrderByDescending(m => m.Value).Select(m => (int?)m.Key).FirstOrDefault() is { } code
            ? NameOf(code)
            : $"Питомец {pet}";

    private PetBook Load()
    {
        try
        {
            if (File.Exists(path) && JsonSerializer.Deserialize<List<PetBook.Entry>>(File.ReadAllText(path), Json) is { } saved)
                return PetBook.FromSnapshot(saved);
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "Pets could not be read; starting empty");
        }
        return new PetBook();
    }

    private void Save()
    {
        try
        {
            var tmp = path + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(book.Snapshot(), Json));
            File.Move(tmp, path, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogDebug(ex, "Pets could not be saved");
        }
    }

    public void Dispose()
    {
        CombatFeed.Damage -= OnDamage;
        CombatFeed.MobDeath -= OnMobDeath;
    }
}
