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
    public GrindSession Grind { get; } = new();
    public HealTally Heals { get; } = new();
    public DeathRecaps Deaths { get; } = new();

    private readonly AionDpsMeter.Services.Services.Entity.EntityTracker entities;

    public MeterModesService(TimersOptionsStore options, AionDpsMeter.Services.Services.Entity.EntityTracker entities)
    {
        this.options = options;
        this.entities = entities;
        dummySeconds = options.Current.DummyRunSeconds;
        Dummy = new DummyRun(TimeSpan.FromSeconds(dummySeconds));
        pvpIdle = options.Current.FightIdleSeconds;
        Pvp = new PvpLog(TimeSpan.FromSeconds(pvpIdle));
        options.Changed += OnOptionsChanged;
        CombatFeed.Damage += OnDamage;
        CombatFeed.Heal += OnHeal;
        CombatFeed.Hit += OnHit;
        CombatFeed.Death += OnDeath;
    }

    /// <summary>Hits on the meter's players, named now (the attacker may be gone by the time someone looks).</summary>
    private void OnHit(RawHit h)
    {
        if (h.Damage <= 0 || entities.GetPlayerEntity(h.TargetId) is null) return;
        var data = AionDpsMeter.Core.GameData.Services.GameDataProvider.Instance;
        if (data.IsHealingSkill(h.SkillCode)) return; // heals come in the same packet as hits
        var source = entities.GetPlayerEntity(h.ActorId)?.Name is { Length: > 0 } player ? player
            : entities.GetTargetMob(h.ActorId) is { MobCode: > 0 } mob ? mob.Name
            : "Неизвестно";
        var skill = data.GetSkillOrDefault(h.SkillCode)?.Name ?? "";
        if (skill.StartsWith("Unknown", StringComparison.OrdinalIgnoreCase)) skill = ""; // monster skills have no names in the data
        Deaths.Add(new IncomingHit(h.TargetId, source, skill, h.Damage, h.Critical, h.At));
    }

    private void OnDeath(int entityId, DateTime at) => Deaths.Died(entityId, at);

    private void OnOptionsChanged()
    {
        if (options.Current.FightIdleSeconds != pvpIdle)
        {
            pvpIdle = options.Current.FightIdleSeconds;
            Pvp = new PvpLog(TimeSpan.FromSeconds(pvpIdle));
        }
        if (options.Current.DummyRunSeconds == dummySeconds) return;
        dummySeconds = options.Current.DummyRunSeconds;
        Dummy = new DummyRun(TimeSpan.FromSeconds(dummySeconds));
    }

    private void OnDamage(PlayerDamage d)
    {
        if (d.SourceEntity is null || d.Damage <= 0) return;
        var hit = new Hit(d.SourceEntity.Id, d.Damage, d.TargetEntity?.IsDummy == true, d.DateTime, d.SourceEntity.IsUser);
        Dummy.Add(hit);
        Grind.Add(hit);
        if (d.TargetEntity is { } target && entities.GetPlayerEntity(target.Id) is { } victim
            && PvpLog.IsPlayerTarget(victim, entities.GetTargetMob(target.Id), entities.IsSummon(target.Id)))
            Pvp.Add(new PvpHit(d.SourceEntity.Id, victim.Id, d.Skill?.Id ?? 0, string.IsNullOrEmpty(d.Skill?.Name) ? "?" : d.Skill.Name,
                d.Damage, d.IsCritical, d.DateTime, d.SourceEntity.IsUser || victim.IsUser, d.Skill is { HasIcon: true } s ? s.Icon : null));
    }

    private void OnHeal(HealEvent h) => Heals.Add(new Heal(h.CasterId, h.TargetId, h.Amount, h.At));

    public void Dispose()
    {
        CombatFeed.Damage -= OnDamage;
        CombatFeed.Heal -= OnHeal;
        CombatFeed.Hit -= OnHit;
        CombatFeed.Death -= OnDeath;
        options.Changed -= OnOptionsChanged;
    }
}
