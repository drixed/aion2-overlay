using AionDpsMeter.Core.Models;
using AionDpsMeter.Services.PacketProcessing.Fork;
using AionDpsMeter.Timers.Feed;

namespace AionDpsMeter.Timers.Modes;

/// <summary>Feeds the Dummy, Grind and Heal modes from every hit and heal the meter parses.</summary>
public sealed class MeterModesService : IDisposable
{
    private readonly TimersOptionsStore options;
    private int dummySeconds, pvpIdle;

    public DummyRun Dummy { get; private set; }
    public PvpLog Pvp { get; private set; }
    /// <summary>The fight summary ("Итоги").</summary>
    public FightLog Fight { get; private set; }
    public GrindSession Grind { get; } = new();
    public HealTally Heals { get; } = new();
    public DeathRecaps Deaths { get; } = new();

    // fight-history.json: the fight summaries (healing, damage taken) for the history window, kept over restarts
    private static string ArchivePath => Path.Combine(AppContext.BaseDirectory, "fight-history.json");
    private readonly Lock archiveGate = new();
    private List<FightSummary> archive = [];
    private Timer? archiveTimer;

    /// <summary>The fight that overlaps a history session: from this run first, else from fight-history.json.</summary>
    public FightSummary? FightFor(DateTime sessionStart, DateTime sessionEnd)
    {
        if (FightArchive.Find(Fight.History(), sessionStart, sessionEnd) is { } live) return live;
        lock (archiveGate) return FightArchive.Find(archive, sessionStart, sessionEnd);
    }

    private static (DateTime, TimeSpan, long, long, long, int) Key(FightSummary f) =>
        (f.Start, f.Duration, f.TotalDamage, f.TotalHeal, f.TotalTaken, f.Players.Count);

    private void SaveFights()
    {
        try
        {
            lock (archiveGate)
            {
                var merged = FightArchive.Merge(archive, Fight.History());
                if (merged.Select(Key).SequenceEqual(archive.Select(Key))) return; // nothing new: no write
                archive = merged;
                File.WriteAllText(ArchivePath, FightArchive.Serialize(archive));
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }

    private readonly AionDpsMeter.Services.Services.Entity.EntityTracker entities;

    public MeterModesService(TimersOptionsStore options, AionDpsMeter.Services.Services.Entity.EntityTracker entities)
    {
        this.options = options;
        this.entities = entities;
        dummySeconds = options.Current.DummyRunSeconds;
        Dummy = new DummyRun(TimeSpan.FromSeconds(dummySeconds));
        pvpIdle = options.Current.FightIdleSeconds;
        Pvp = new PvpLog(TimeSpan.FromSeconds(pvpIdle));
        Fight = new FightLog(TimeSpan.FromSeconds(pvpIdle));
        options.Changed += OnOptionsChanged;
        CombatFeed.Damage += OnDamage;
        CombatFeed.Heal += OnHeal;
        CombatFeed.Hit += OnHit;
        CombatFeed.Death += OnDeath;
        entities.SummonRegistered += OnSummonRegistered;
        try { if (File.Exists(ArchivePath)) archive = FightArchive.Parse(File.ReadAllText(ArchivePath)); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        archiveTimer = new Timer(_ => SaveFights(), null, TimeSpan.FromSeconds(20), TimeSpan.FromSeconds(20));
    }

    private void OnSummonRegistered(int summonId, int ownerId)
    {
        Dummy.Merge(summonId, ownerId);
        Grind.Merge(summonId, ownerId);
    }

    /// <summary>Hits on the meter's players, named now (the attacker may be gone by the time someone looks).</summary>
    private void OnHit(RawHit h)
    {
        if (h.Damage <= 0 || entities.GetPlayerEntity(h.TargetId) is null) return;
        // the boss and adds can be in the player list too (they once hit with a skill): their "damage taken" is the group's damage
        if (entities.GetTargetMob(h.TargetId) is { MobCode: > 0 } || entities.IsSummon(h.TargetId)) return;
        var data = AionDpsMeter.Core.GameData.Services.GameDataProvider.Instance;
        if (data.IsHealingSkill(h.SkillCode)) return; // heals come in the same packet as hits
        var source = entities.GetPlayerEntity(h.ActorId)?.Name is { Length: > 0 } player ? player
            : entities.GetTargetMob(h.ActorId) is { MobCode: > 0 } mob ? mob.Name
            : "Неизвестно";
        var skill = data.GetSkillOrDefault(h.SkillCode)?.Name ?? "";
        if (skill.StartsWith("Unknown", StringComparison.OrdinalIgnoreCase)) skill = ""; // monster skills have no names in the data
        Deaths.Add(new IncomingHit(h.TargetId, source, skill, h.Damage, h.Critical, h.At));
        Fight.AddTaken(h.TargetId, h.Damage, h.At);
    }

    private void OnDeath(int entityId, DateTime at)
    {
        Deaths.Died(entityId, at);
        Pvp.Died(entityId, at);
        Fight.Died(entityId, at);
    }

    private void OnOptionsChanged()
    {
        if (options.Current.FightIdleSeconds != pvpIdle)
        {
            pvpIdle = options.Current.FightIdleSeconds;
            Pvp = new PvpLog(TimeSpan.FromSeconds(pvpIdle));
            Fight = new FightLog(TimeSpan.FromSeconds(pvpIdle));
        }
        if (options.Current.DummyRunSeconds == dummySeconds) return;
        dummySeconds = options.Current.DummyRunSeconds;
        Dummy = new DummyRun(TimeSpan.FromSeconds(dummySeconds));
    }

    private void OnDamage(PlayerDamage d)
    {
        if (d.SourceEntity is null || d.Damage <= 0) return;
        // a summoned object's hit (the session gives it to its owner later, the feed comes first): counted as the owner's
        var sourceId = entities.GetSummonOwner(d.SourceEntity.Id) is { } owner ? owner : d.SourceEntity.Id;
        var mine = sourceId == d.SourceEntity.Id ? d.SourceEntity.IsUser : entities.GetPlayerEntity(sourceId)?.IsUser == true;
        var hit = new Hit(sourceId, d.Damage, d.TargetEntity?.IsDummy == true, d.DateTime, mine);
        Dummy.Add(hit);
        Grind.Add(hit);
        // a hit on a monster (not a player, not a training dummy) opens or feeds the fight summary
        if (d.TargetEntity is { IsDummy: false } mob && entities.GetPlayerEntity(mob.Id) is not ({ IsIdentified: true } or { IsUser: true }))
            Fight.AddDamage(sourceId, d.Damage, string.IsNullOrEmpty(d.Skill?.Name) ? "?" : d.Skill.Name, d.DateTime,
                mob.MobCode > 0 ? mob.Name : null);
        if (d.TargetEntity is { } target && entities.GetPlayerEntity(target.Id) is { } victim
            && PvpLog.IsPlayerTarget(victim, entities.GetTargetMob(target.Id), entities.IsSummon(target.Id))
            && PvpLog.IsPvpHit(entities.GetPlayerEntity(d.SourceEntity.Id) ?? d.SourceEntity, victim))
            Pvp.Add(new PvpHit(d.SourceEntity.Id, victim.Id, d.Skill?.Id ?? 0, string.IsNullOrEmpty(d.Skill?.Name) ? "?" : d.Skill.Name,
                d.Damage, d.IsCritical, d.DateTime, d.SourceEntity.IsUser || victim.IsUser, d.Skill is { HasIcon: true } s ? s.Icon : null));
    }

    private void OnHeal(HealEvent h)
    {
        Heals.Add(new Heal(h.CasterId, h.TargetId, h.Amount, h.At));
        Fight.AddHeal(h.CasterId, h.TargetId, h.Amount, h.At);
    }

    public void Dispose()
    {
        CombatFeed.Damage -= OnDamage;
        CombatFeed.Heal -= OnHeal;
        CombatFeed.Hit -= OnHit;
        CombatFeed.Death -= OnDeath;
        options.Changed -= OnOptionsChanged;
        entities.SummonRegistered -= OnSummonRegistered;
        archiveTimer?.Dispose();
        SaveFights();
    }
}
