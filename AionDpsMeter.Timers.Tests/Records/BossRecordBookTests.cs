using AionDpsMeter.Timers.Records;

namespace AionDpsMeter.Timers.Tests.Records;

public class BossRecordBookTests
{
    private static readonly DateTime Day = new(2026, 10, 9, 20, 0, 0);

    private static FightResult Kill(string boss, double seconds, long myDamage = 1_000_000, long hp = 8_208_000, long damage = 8_216_291) =>
        new(boss, hp, damage, TimeSpan.FromSeconds(seconds), myDamage, Day);

    [Fact]
    public void The_first_kill_sets_every_record()
    {
        var book = new BossRecordBook();
        var news = book.Add(Kill("Red Spark Ignus", 80));
        Assert.Equal(RecordNews.FirstKill, news);
        var r = book.For("Red Spark Ignus", 8_208_000)!;
        Assert.Equal(1, r.Kills);
        Assert.Equal(TimeSpan.FromSeconds(80), r.BestKill);
        Assert.Equal(8_216_291 / 80.0, r.BestPartyDps, 3);
        Assert.Equal(1_000_000 / 80.0, r.BestMyDps, 3);
    }

    [Fact]
    public void A_faster_kill_beats_the_time_record()
    {
        var book = new BossRecordBook();
        book.Add(Kill("Red Spark Ignus", 80));
        Assert.Equal(RecordNews.FasterKill, book.Add(Kill("Red Spark Ignus", 68)));
        var r = book.For("Red Spark Ignus", 8_208_000)!;
        Assert.Equal(TimeSpan.FromSeconds(68), r.BestKill);
        Assert.Equal(TimeSpan.FromSeconds(80), r.PreviousBestKill);
        Assert.Equal(2, r.Kills);
    }

    [Fact]
    public void A_slower_kill_with_more_own_dps_is_a_personal_record()
    {
        var book = new BossRecordBook();
        book.Add(Kill("Red Spark Ignus", 70, myDamage: 1_000_000));
        Assert.Equal(RecordNews.BetterMyDps, book.Add(Kill("Red Spark Ignus", 75, myDamage: 1_500_000)));
        Assert.Equal(TimeSpan.FromSeconds(70), book.For("Red Spark Ignus", 8_208_000)!.BestKill);
    }

    /// <summary>A click on a record opens that fight's details from the history.</summary>
    [Fact]
    public void Records_remember_the_fight_they_were_set_in()
    {
        var book = new BossRecordBook();
        Guid first = Guid.NewGuid(), faster = Guid.NewGuid(), stronger = Guid.NewGuid();
        book.Add(Kill("Red Spark Ignus", 80, myDamage: 1_000_000) with { SessionId = first });
        book.Add(Kill("Red Spark Ignus", 68, myDamage: 900_000) with { SessionId = faster });
        book.Add(Kill("Red Spark Ignus", 75, myDamage: 1_600_000) with { SessionId = stronger });
        var r = book.For("Red Spark Ignus", 8_208_000)!;
        Assert.Equal(faster, r.BestKillSession);
        Assert.Equal(stronger, r.BestMyDpsSession);
    }

    [Fact]
    public void A_slower_and_weaker_kill_only_counts()
    {
        var book = new BossRecordBook();
        book.Add(Kill("Red Spark Ignus", 70, myDamage: 1_000_000));
        Assert.Equal(RecordNews.None, book.Add(Kill("Red Spark Ignus", 90, myDamage: 900_000)));
        Assert.Equal(2, book.For("Red Spark Ignus", 8_208_000)!.Kills);
    }

    /// <summary>The same boss on another difficulty has other HP (Nuakum 5.7M and 15.5M in the history).</summary>
    [Fact]
    public void Each_difficulty_keeps_its_own_records()
    {
        var book = new BossRecordBook();
        book.Add(Kill("Ferocious Horn Nuakum", 64, hp: 5_700_000, damage: 5_712_151));
        book.Add(Kill("Ferocious Horn Nuakum", 272, hp: 15_504_000, damage: 15_509_566));
        Assert.Equal(TimeSpan.FromSeconds(64), book.For("Ferocious Horn Nuakum", 5_700_000)!.BestKill);
        Assert.Equal(TimeSpan.FromSeconds(272), book.For("Ferocious Horn Nuakum", 15_504_000)!.BestKill);
        Assert.Null(book.For("Ferocious Horn Nuakum", 1_000_000));
    }

    [Theory]
    [InlineData("Red Spark Ignus", 8_208_000, 5_982_726, 58.7)]   // wiped / left: not killed
    [InlineData("Unknown (0)", 6_800_000, 6_802_556, 180)]       // unidentified target
    [InlineData("Training Scarecrow", 9_000_000, 9_000_000, 60)]  // dummy
    [InlineData("Red Spark Ignus", 67, 8_216_291, 80)]           // junk max HP: damage far over it
    [InlineData("Some Mob", 140_000, 140_100, 10)]               // ordinary mob (boss-only off)
    [InlineData("Red Spark Ignus", 8_208_000, 8_216_291, 2)]      // meter saw only the last hits
    public void Only_real_boss_kills_count(string boss, long hp, long damage, double seconds)
    {
        var book = new BossRecordBook();
        Assert.Equal(RecordNews.None, book.Add(new FightResult(boss, hp, damage, TimeSpan.FromSeconds(seconds), 1, Day)));
        Assert.Empty(book.All);
    }

    [Fact]
    public void Pace_compares_the_projected_kill_with_the_record()
    {
        var record = new BossRecordBook();
        record.Add(Kill("Red Spark Ignus", 80));
        var r = record.For("Red Spark Ignus", 8_208_000)!;
        Assert.Equal(TimeSpan.FromSeconds(-8), BossPace.Delta(r, elapsed: TimeSpan.FromSeconds(50), toKill: TimeSpan.FromSeconds(22)));
        Assert.Equal(TimeSpan.FromSeconds(15), BossPace.Delta(r, elapsed: TimeSpan.FromSeconds(60), toKill: TimeSpan.FromSeconds(35)));
        Assert.Null(BossPace.Delta(r, TimeSpan.FromSeconds(60), toKill: null));
        Assert.Null(BossPace.Delta(null, TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(35)));
    }
}

public class RecordTextTests
{
    [Theory]
    [InlineData(-8, "быстрее рекорда на 0:08")]
    [InlineData(15, "медленнее рекорда на 0:15")]
    [InlineData(0.4, "в темпе рекорда")]
    [InlineData(-75, "быстрее рекорда на 1:15")]
    public void Pace_reads_as_words(double seconds, string expected) =>
        Assert.Equal(expected, RecordText.Pace(TimeSpan.FromSeconds(seconds)));

    [Theory]
    [InlineData(68, "1:08")]
    [InlineData(272.6, "4:32")]
    [InlineData(3725, "1:02:05")]
    public void Kill_times_read_as_minutes_and_seconds(double seconds, string expected) =>
        Assert.Equal(expected, RecordText.Time(TimeSpan.FromSeconds(seconds)));
}
