using AionDpsMeter.Timers.Bosses;

namespace AionDpsMeter.Timers.Tests.Bosses;

/// <summary>Krao Cave, EU 2026-11-20: the boss (entity 22258) winds up "Волна драны" (1800060) at a player for 2 s, and an
/// "Атака" (1800053) around itself (aimed at itself) for 6 s. Frames from the opcode on.</summary>
public class BossCastTests
{
    private const string WaveAtPlayer = "0938F2AD01027C771B00010290636EB19EC2C2E3F6C5EDF978C2F74E2BC5D00F01904E";
    private const string AroundItself = "0938F2AD010275771B000102F2AD0100000000003017C60000E74300002BC5F02E01904E";
    private static readonly DateTime T0 = new(2026, 11, 20, 16, 32, 1);

    [Fact]
    public void A_windup_names_caster_skill_target_and_cast_time()
    {
        var cast = CastParser.Windup(T0, Convert.FromHexString(WaveAtPlayer))!;
        Assert.Equal(new BossCast(22258, 1800060, 12688, T0, TimeSpan.FromMilliseconds(2000)), cast);
        var around = CastParser.Windup(T0, Convert.FromHexString(AroundItself))!;
        Assert.Equal(22258, around.Target);
        Assert.True(around.AroundCaster);
        Assert.Equal(TimeSpan.FromSeconds(6), around.Duration);
        Assert.Null(CastParser.Windup(T0, Convert.FromHexString("0938F2AD01")));
    }

    [Fact]
    public void Only_watched_casters_count_and_a_cast_goes_away_soon_after_it_ends()
    {
        var casts = new BossCastTracker(caster => caster == 22258);
        casts.OnFrame(T0, Convert.FromHexString(WaveAtPlayer));
        casts.OnFrame(T0, Convert.FromHexString("0938B0B002027C771B00010290636EB19EC2C2E3F6C5EDF978C2F74E2BC5D00F01904E")); // an add (another caster)
        Assert.Equal([1800060], casts.Active(T0.AddSeconds(1)).Select(c => c.Skill));
        Assert.Single(casts.Active(T0.AddSeconds(2.3)));                       // just ended: still there a moment
        Assert.Empty(casts.Active(T0.AddSeconds(3)));
    }

    [Fact]
    public void A_skill_that_once_took_most_of_my_hp_is_lethal()
    {
        using var dir = new TempDir();
        var casts = new BossCastTracker(_ => true, dir.File("boss-hits.json"));
        casts.OnHitOnMe(1800060, 61_000);
        casts.OnHitOnMe(1800060, 40_000);                                      // the biggest is kept
        casts.OnHitOnMe(1800053, 20_000);
        Assert.Equal(CastDanger.Lethal, casts.Danger(1800060, 64_000));
        Assert.Equal(CastDanger.Dangerous, casts.Danger(1800053, 50_000));
        Assert.Equal(CastDanger.None, casts.Danger(1800053, 200_000));
        Assert.Equal(CastDanger.None, casts.Danger(1, 64_000));                 // never hit me: unknown
        Assert.Equal(61_000, new BossCastTracker(_ => true, dir.File("boss-hits.json")).MaxHit(1800060));
    }
}
