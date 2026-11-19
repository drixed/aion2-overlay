using AionDpsMeter.Timers.Modes;

namespace AionDpsMeter.Timers.Tests.Modes;

public class FightLogTests
{
    private static readonly DateTime T0 = new(2026, 11, 20, 1, 0, 0);
    private const long Tank = 1, Healer = 2, Dps = 3;

    private static DateTime At(double s) => T0.AddSeconds(s);

    private static FightLog Fight()
    {
        var log = new FightLog(TimeSpan.FromSeconds(45));
        log.AddDamage(Tank, 1_000, "Удар щитом", At(0));
        log.AddDamage(Dps, 9_000, "Удар с вращением", At(1.1));
        log.AddDamage(Dps, 3_000, "Разрез", At(5));
        log.AddHeal(Healer, Tank, 4_000, At(6));
        log.AddHeal(Healer, Healer, 500, At(7));
        log.AddTaken(Tank, 6_000, At(8));
        log.AddTaken(Dps, 2_000, At(9));
        log.Died(Dps, At(10));
        log.AddDamage(Healer, 200, "Молния", At(20));
        return log;
    }

    [Fact]
    public void The_summary_has_each_player_s_damage_healing_taken_and_deaths()
    {
        var s = Fight().Summary()!;
        Assert.Equal(TimeSpan.FromSeconds(20), s.Duration);
        var dps = s.Players.Single(p => p.Id == Dps);
        Assert.Equal((12_000L, 2_000L, 1, 9_000L, "Удар с вращением"), (dps.Damage, dps.DamageTaken, dps.Deaths, dps.MaxHit, dps.MaxHitSkill));
        var healer = s.Players.Single(p => p.Id == Healer);
        Assert.Equal((4_500L, 500L, 500L), (healer.HealDone, healer.HealReceived, healer.SelfHeal));
        Assert.Equal(4_000, s.Players.Single(p => p.Id == Tank).HealReceived);
    }

    [Fact]
    public void Totals_and_who_opened_the_fight()
    {
        var s = Fight().Summary()!;
        Assert.Equal((12_000L + 1_000 + 200, 4_500L, 8_000L, 1), (s.TotalDamage, s.TotalHeal, s.TotalTaken, s.TotalDeaths));
        Assert.Equal(TimeSpan.Zero, s.Players.Single(p => p.Id == Tank).FirstHit);
        Assert.Equal(TimeSpan.FromSeconds(1.1), s.Players.Single(p => p.Id == Dps).FirstHit);
    }

    [Fact]
    public void Heals_and_hits_before_the_first_damage_do_not_open_a_fight()
    {
        var log = new FightLog(TimeSpan.FromSeconds(45));
        log.AddHeal(Healer, Tank, 4_000, At(0));
        log.AddTaken(Tank, 4_000, At(1));
        Assert.Null(log.Summary());
    }

    [Fact]
    public void A_pause_longer_than_the_idle_time_starts_a_new_fight_and_the_last_one_stays()
    {
        var log = Fight();
        Assert.Equal(13_200, log.Summary()!.TotalDamage);
        log.AddDamage(Tank, 50, "Удар щитом", At(200));
        Assert.Equal(50, log.Summary()!.TotalDamage);
    }

    [Fact]
    public void Clearing_drops_the_fight()
    {
        var log = Fight();
        log.Clear();
        Assert.Null(log.Summary());
    }

    [Fact]
    public void Past_fights_are_kept_newest_first_and_named_by_the_most_damaged_target()
    {
        var log = new FightLog(TimeSpan.FromSeconds(45));
        log.AddDamage(Dps, 1_000, "Разрез", At(0), "Голем");
        log.AddDamage(Dps, 5_000, "Разрез", At(1), "Генерал Рагта");
        log.AddDamage(Dps, 700, "Разрез", At(300), "Вечный Гартуа");
        var history = log.History();
        Assert.Equal(["Вечный Гартуа", "Генерал Рагта"], history.Select(f => f.Target));
        Assert.Equal(6_000, history[1].TotalDamage);
    }

    [Fact]
    public void Only_the_last_twenty_fights_are_kept()
    {
        var log = new FightLog(TimeSpan.FromSeconds(45));
        for (var i = 0; i < 25; i++) log.AddDamage(Dps, 100, "Разрез", At(i * 100), $"Моб {i}");
        Assert.Equal(20, log.History().Count);
        Assert.Equal("Моб 24", log.History()[0].Target);
    }
}
