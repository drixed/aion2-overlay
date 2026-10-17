using AionDpsMeter.Timers.Modes;

namespace AionDpsMeter.Timers.Tests.Modes;

public class PvpLogTests
{
    private static readonly DateTime T0 = new(2026, 10, 17, 15, 0, 0);
    private const long Me = 1, Foe = 2, Other = 3;

    private static PvpHit Hit(long attacker, long victim, string skill, long damage, double atSeconds, bool crit = false) =>
        new(attacker, victim, skill.Length, skill, damage, crit, T0.AddSeconds(atSeconds), Mine: attacker == Me || victim == Me);

    [Fact]
    public void Rows_are_damage_dealt_to_players_with_dps_over_the_fight()
    {
        var log = new PvpLog(TimeSpan.FromSeconds(45));
        log.Add(Hit(Me, Foe, "Удар", 3_000, 0));
        log.Add(Hit(Foe, Me, "Пинок", 1_000, 5));
        log.Add(Hit(Me, Foe, "Удар", 3_000, 10));

        var view = log.View(T0.AddSeconds(10));
        Assert.True(view.HasFight);
        Assert.Equal(TimeSpan.FromSeconds(10), view.Elapsed);
        Assert.Equal([Me, Foe], view.Rows.Select(r => r.PlayerId));
        Assert.Equal(600, view.Rows[0].PerSecond, 3);
    }

    [Fact]
    public void Dealt_and_received_are_broken_down_by_skill()
    {
        var log = new PvpLog(TimeSpan.FromSeconds(45));
        log.Add(Hit(Me, Foe, "Удар", 3_000, 0, crit: true));
        log.Add(Hit(Me, Foe, "Удар", 2_000, 1));
        log.Add(Hit(Me, Foe, "Рывок", 9_000, 2));
        log.Add(Hit(Foe, Me, "Пинок", 1_000, 3));
        log.Add(Hit(Other, Me, "Стрела", 4_000, 4));

        Assert.Equal([new PvpSkillRow("Рывок", 9_000, 1, 0), new PvpSkillRow("Удар", 5_000, 2, 1)], log.Dealt(Me));
        Assert.Equal([new PvpReceivedRow(Other, "Стрела", 4_000, 1), new PvpReceivedRow(Foe, "Пинок", 1_000, 1)], log.Received(Me));
    }

    [Fact]
    public void A_pause_longer_than_the_idle_time_starts_a_new_fight_and_the_last_one_stays_until_then()
    {
        var log = new PvpLog(TimeSpan.FromSeconds(45));
        log.Add(Hit(Me, Foe, "Удар", 3_000, 0));
        log.Add(Hit(Me, Foe, "Удар", 3_000, 20));

        var after = log.View(T0.AddSeconds(300));                 // long after: the last fight is still shown
        Assert.False(after.Active);
        Assert.Equal(TimeSpan.FromSeconds(20), after.Elapsed);
        Assert.Equal(6_000, after.Rows.Single().Total);

        log.Add(Hit(Foe, Me, "Пинок", 500, 400));
        var next = log.View(T0.AddSeconds(400));
        Assert.Equal(500, next.Rows.Single().Total);
        Assert.Empty(log.Dealt(Me));
    }

    [Fact]
    public void Only_fights_with_me_in_them_switch_the_mode_on()
    {
        var log = new PvpLog(TimeSpan.FromSeconds(45));
        log.Add(Hit(Foe, Other, "Пинок", 500, 0));
        Assert.False(log.MineActive(T0.AddSeconds(1)));
        log.Add(Hit(Foe, Me, "Пинок", 500, 2));
        Assert.True(log.MineActive(T0.AddSeconds(30)));
        Assert.False(log.MineActive(T0.AddSeconds(60)));             // 45 s without PvP involving me
    }

    [Fact]
    public void Only_a_named_player_or_the_user_counts_as_a_pvp_target()
    {
        // 2026-10-17: "Player_17921" rows — hits on monsters the meter had once taken for players
        var named = new AionDpsMeter.Core.Models.Player { Id = 5, Name = "Rafff", IsIdentified = true };
        var unnamed = new AionDpsMeter.Core.Models.Player { Id = 6, Name = "Player_6" };
        var me = new AionDpsMeter.Core.Models.Player { Id = 7, Name = "DRIXED", IsUser = true };
        Assert.True(PvpLog.IsPlayerTarget(named, asMob: null, summon: false));
        Assert.True(PvpLog.IsPlayerTarget(me, asMob: null, summon: false));
        Assert.False(PvpLog.IsPlayerTarget(unnamed, asMob: null, summon: false));
        Assert.False(PvpLog.IsPlayerTarget(named, asMob: new AionDpsMeter.Core.Models.Mob { MobCode = 2_100_855 }, summon: false));
        Assert.False(PvpLog.IsPlayerTarget(named, asMob: null, summon: true));
        Assert.False(PvpLog.IsPlayerTarget(null, asMob: null, summon: false));
    }

    [Fact]
    public void Rows_carry_the_skill_icon()
    {
        var log = new PvpLog(TimeSpan.FromSeconds(45));
        log.Add(new PvpHit(Me, Foe, 7, "Удар", 3_000, false, T0, Mine: true, Icon: "https://cdn/skill7.png"));
        Assert.Equal("https://cdn/skill7.png", log.Dealt(Me).Single().Icon);
        Assert.Equal("https://cdn/skill7.png", log.Received(Foe).Single().Icon);
    }

    [Fact]
    public void Clearing_drops_the_fight_and_leaves_the_pvp_mode()
    {
        var log = new PvpLog(TimeSpan.FromSeconds(45));
        log.Add(Hit(Foe, Me, "Пинок", 500, 0));
        log.Clear();
        Assert.False(log.View(T0.AddSeconds(1)).HasFight);
        Assert.False(log.MineActive(T0.AddSeconds(1)));
        Assert.Empty(log.Received(Me));
    }

    [Fact]
    public void Without_any_pvp_there_is_no_fight()
    {
        var view = new PvpLog(TimeSpan.FromSeconds(45)).View(T0);
        Assert.False(view.HasFight);
        Assert.Empty(view.Rows);
    }
}
