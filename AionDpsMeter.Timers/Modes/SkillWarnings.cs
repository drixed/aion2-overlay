// aion2-overlay fork: new file (see FORK_CHANGES.md).
namespace AionDpsMeter.Timers.Modes;

/// <summary>"Предупреждения о крите и точности": marks in the skill table for skills that rarely crit or are often parried.</summary>
public static class SkillWarnings
{
    private const int MinHits = 5;

    /// <summary>Crits under half the player's own crit rate (both in %), once there are enough hits to tell.</summary>
    public static bool RareCrit(double skillCrit, double playerCrit, int hits) =>
        hits >= MinHits && playerCrit >= 10 && skillCrit < playerCrit / 2;

    /// <summary>One hit in ten or more parried (in %).</summary>
    public static bool OftenParried(double parryRate, int hits) => hits >= MinHits && parryRate >= 10;
}
