using AionDpsMeter.Timers.Modes;

namespace AionDpsMeter.Timers.Tests.Modes;

public class SkillWarningsTests
{
    [Fact]
    public void A_skill_that_crits_under_half_the_player_average_is_marked()
    {
        Assert.True(SkillWarnings.RareCrit(skillCrit: 20, playerCrit: 50, hits: 10));
        Assert.False(SkillWarnings.RareCrit(skillCrit: 30, playerCrit: 50, hits: 10));
        Assert.False(SkillWarnings.RareCrit(skillCrit: 0, playerCrit: 50, hits: 4)); // too few hits to tell
        Assert.False(SkillWarnings.RareCrit(skillCrit: 0, playerCrit: 4, hits: 50));  // the player barely crits at all
    }

    [Fact]
    public void A_skill_parried_one_hit_in_ten_or_more_is_marked()
    {
        Assert.True(SkillWarnings.OftenParried(parryRate: 10, hits: 10));
        Assert.False(SkillWarnings.OftenParried(parryRate: 9.9, hits: 10));
        Assert.False(SkillWarnings.OftenParried(parryRate: 50, hits: 2));
    }
}
