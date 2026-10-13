using AionDpsMeter.Core.Models;
using AionDpsMeter.Services.PacketProcessing.Fork;
using AionDpsMeter.Timers.Feed;

namespace AionDpsMeter.Timers.Modes;

/// <summary>Feeds the Dummy, Grind and Heal modes from every hit and heal the meter parses.</summary>
public sealed class MeterModesService : IDisposable
{
    private readonly TimersOptionsStore options;
    private int dummySeconds;

    public DummyRun Dummy { get; private set; }
    public GrindSession Grind { get; } = new();
    public HealTally Heals { get; } = new();

    public MeterModesService(TimersOptionsStore options)
    {
        this.options = options;
        dummySeconds = options.Current.DummyRunSeconds;
        Dummy = new DummyRun(TimeSpan.FromSeconds(dummySeconds));
        options.Changed += OnOptionsChanged;
        CombatFeed.Damage += OnDamage;
        CombatFeed.Heal += OnHeal;
    }

    private void OnOptionsChanged()
    {
        if (options.Current.DummyRunSeconds == dummySeconds) return;
        dummySeconds = options.Current.DummyRunSeconds;
        Dummy = new DummyRun(TimeSpan.FromSeconds(dummySeconds));
    }

    private void OnDamage(PlayerDamage d)
    {
        if (d.SourceEntity is null || d.Damage <= 0) return;
        var hit = new Hit(d.SourceEntity.Id, d.Damage, d.TargetEntity?.IsDummy == true, d.DateTime);
        Dummy.Add(hit);
        Grind.Add(hit);
    }

    private void OnHeal(HealEvent h) => Heals.Add(new Heal(h.CasterId, h.TargetId, h.Amount, h.At));

    public void Dispose()
    {
        CombatFeed.Damage -= OnDamage;
        CombatFeed.Heal -= OnHeal;
        options.Changed -= OnOptionsChanged;
    }
}
