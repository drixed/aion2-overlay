using AionDpsMeter.Timers.Modes;

namespace AionDpsMeter.Timers.Tests.Modes;

public class FightArchiveTests
{
    private static readonly DateTime T0 = new(2026, 11, 20, 18, 29, 0);

    private static FightSummary Fight(double startMinutes, double seconds, long heal) =>
        new(T0.AddMinutes(startMinutes), TimeSpan.FromSeconds(seconds),
            [new FightPlayer(7, 1_000, heal, 0, 0, 500, 0, 300, "Огненная стрела", TimeSpan.Zero)], "Tiere");

    [Fact]
    public void A_fight_still_going_is_replaced_by_its_longer_version_not_kept_twice()
    {
        var kept = FightArchive.Merge([], [Fight(0, 10, 100)]);
        kept = FightArchive.Merge(kept, [Fight(0, 55, 900), Fight(5, 20, 50)]);
        Assert.Equal(2, kept.Count);
        Assert.Equal(900, kept.Single(f => f.Start == T0).Players[0].HealDone);
        Assert.Equal(T0.AddMinutes(5), kept[0].Start); // newest first
    }

    [Fact]
    public void Only_the_newest_fights_are_kept()
    {
        var many = Enumerable.Range(0, FightArchive.Kept + 10).Select(i => Fight(i, 10, i)).ToList();
        var kept = FightArchive.Merge([], many);
        Assert.Equal(FightArchive.Kept, kept.Count);
        Assert.Equal(T0.AddMinutes(FightArchive.Kept + 9), kept[0].Start);
    }

    [Fact]
    public void A_saved_fight_is_found_for_the_history_session_it_overlaps()
    {
        var kept = FightArchive.Merge([], [Fight(0, 55, 900), Fight(30, 40, 10)]);
        Assert.Equal(900, FightArchive.Find(kept, T0.AddSeconds(2), T0.AddSeconds(57))?.Players[0].HealDone);
        Assert.Null(FightArchive.Find(kept, T0.AddMinutes(10), T0.AddMinutes(11)));
    }

    [Fact]
    public void The_archive_survives_a_restart()
    {
        var kept = FightArchive.Merge([], [Fight(0, 55, 900)]);
        var back = FightArchive.Parse(FightArchive.Serialize(kept));
        Assert.Equal(kept[0].Players[0], back[0].Players[0]);
        Assert.Equal((kept[0].Start, kept[0].Duration, kept[0].Target), (back[0].Start, back[0].Duration, back[0].Target));
    }

    [Fact]
    public void A_broken_file_gives_an_empty_archive() => Assert.Empty(FightArchive.Parse("{oops"));
}
