using AionDpsMeter.Core.Models;

namespace AionDpsMeter.Timers.Overlay;

/// <summary>
/// Which unidentified targets (spawn not seen, code 0) count as bosses. Started mid-fight, the target being hit right
/// away is the boss; anything first hit later is an ordinary mob that was already standing there.
/// </summary>
public static class UnknownBossPolicy
{
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(1);

    public static Func<Mob, bool> FirstMinuteAfter(DateTime started) => mob => mob.CreatedAt <= started + Window;
}
