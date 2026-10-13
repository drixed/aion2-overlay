using AionDpsMeter.Timers.Modes;

namespace AionDpsMeter.Timers.Tests.Modes;

public class DeathRecapTests
{
    private static readonly DateTime T0 = new(2026, 10, 13, 12, 0, 0);

    private static IncomingHit Hit(long target, string source, long damage, double at, bool crit = false) =>
        new(target, source, "Удар", damage, crit, T0.AddSeconds(at));

    [Fact]
    public void A_death_keeps_the_last_six_seconds_of_hits_in_order()
    {
        var recaps = new DeathRecaps();
        recaps.Add(Hit(7, "Судья Урахум", 1_000, 0));     // 10 s before: too early
        recaps.Add(Hit(7, "Судья Урахум", 30_000, 5));
        recaps.Add(Hit(8, "Судья Урахум", 99_999, 6));    // someone else
        recaps.Add(Hit(7, "Миньон", 12_000, 8, crit: true));
        recaps.Add(Hit(7, "Судья Урахум", 45_000, 9.5));
        recaps.Died(7, T0.AddSeconds(10));

        var recap = recaps.LastFor(7)!;
        Assert.Equal(T0.AddSeconds(10), recap.At);
        Assert.Equal([30_000L, 12_000L, 45_000L], recap.Hits.Select(h => h.Damage));
        Assert.Equal(87_000, recap.Total);
        Assert.Equal("Судья Урахум", recap.KillingBlow!.Source);
        Assert.Equal(-5, (recap.Hits[0].At - recap.At).TotalSeconds, 3);
    }

    [Fact]
    public void A_death_without_hits_still_shows_up()
    {
        var recaps = new DeathRecaps();
        recaps.Died(7, T0);
        Assert.Empty(recaps.LastFor(7)!.Hits);
        Assert.Null(recaps.LastFor(8));
    }

    [Fact]
    public void Old_hits_are_forgotten()
    {
        var recaps = new DeathRecaps();
        recaps.Add(Hit(7, "Моб", 5_000, 0));
        recaps.Add(Hit(9, "Моб", 1, 120)); // two minutes later, another player
        recaps.Died(7, T0.AddSeconds(3));   // a late death report still finds the hit within six seconds
        Assert.Single(recaps.LastFor(7)!.Hits);
    }
}
