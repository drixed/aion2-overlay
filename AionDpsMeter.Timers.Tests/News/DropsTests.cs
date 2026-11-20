using AionDpsMeter.Timers.News;

namespace AionDpsMeter.Timers.Tests.News;

/// <summary>NC's notices (EU board, 2026): the Halloween drops (one "Event Schedule" with zones, an "All Channels" table) and the
/// Ludra notice (drops in the middle of an event notice, a schedule with "PST:" in front and no zone after the times).</summary>
public class DropsTests
{
    private const string Halloween =
        "<div><p>■ Event Schedule</p><p>PDT/PST: October 28, 2026 at 10:00 PDT - November 3, 2026 at 23:59 PST</p>" +
        "<p>CET: October 28, 2026 at 18:00 - November 4, 2026 at 08:59</p><p>■ Event Details</p><p>All Channels</p>" +
        "<table><tr><td>Watch Time</td><td>Reward</td><td>Qty</td></tr><tr><td>1 hr</td><td>Resurrection Spiritstone (Bound)</td><td>x15</td></tr>" +
        "<tr><td>2 hr</td><td>Appearance Change Voucher (Bound)</td><td>x1</td></tr><tr><td>8 hr</td><td>Chubby Panda (Helm)</td><td>x1</td></tr></table>" +
        "<p>Claim the Twitch drops from your inventory before November 17, 2026 at 23:59 PST / November 18, 2026 at 08:59 CET.</p></div>";

    private const string Ludra =
        "<div><p>■ Event Schedule</p><p>PST: November 4, 2026 at 00:00 - November 17, 2026 at 23:59</p><p>Login rewards ...</p>" +
        "<p>Claim your Odyle Energy x4</p><p>Twitch Drops</p><p>■ Event Schedule</p><p>PST: November 4, 2026 at 06:00 - November 17, 2026 at 23:59</p>" +
        "<p>CET: November 4, 2026 at 15:00 - November 18, 2026 at 8:59</p><p>■ Event Details</p>" +
        "<table><tr><td>Watch Time</td><td>Reward</td><td>Qty</td></tr><tr><td>1 hr</td><td>Battle Support Chest (Bound)</td><td>x1</td></tr>" +
        "<tr><td>2 hr</td><td>Skin Chest (10 Times) (Bound)</td><td>x3</td></tr><tr><td>14 hr</td><td>Black Conical Hat (Skin: Helm)</td><td>x1</td></tr></table>" +
        "<p>Chest Contents Qty</p><p>Battle Support Chest (Bound) Resurrection Spiritstone (Bound) x15</p><p>Check out our Twitch Drops FAQ for more details.</p></div>";

    [Fact]
    public void A_drops_notice_gives_the_campaign_with_its_period_and_rewards()
    {
        var (claimBy, campaigns) = DropsParser.Parse(Halloween, "6adb", new DateTimeOffset(2026, 10, 28, 0, 0, 0, TimeSpan.Zero),
            "Global Halloween Twitch Drops: October 28 - November 3 PT");
        var c = Assert.Single(campaigns);
        Assert.Equal("Halloween", c.Name);
        Assert.Equal(new DateTimeOffset(2026, 10, 28, 17, 0, 0, TimeSpan.Zero), c.Start);      // 10:00 PDT
        Assert.Equal(new DateTimeOffset(2026, 11, 4, 7, 59, 0, TimeSpan.Zero), c.End);         // 23:59 PST
        Assert.Equal([(60, "Resurrection Spiritstone (Bound)", 15), (120, "Appearance Change Voucher (Bound)", 1), (480, "Chubby Panda (Helm)", 1)],
            c.Rewards.Select(r => (r.Minutes, r.Name, r.Qty)));
        Assert.Equal(new DateTimeOffset(2026, 11, 18, 7, 59, 0, TimeSpan.Zero), claimBy);
    }

    [Fact]
    public void Drops_inside_an_event_notice_take_the_schedule_right_before_their_table()
    {
        var (claimBy, campaigns) = DropsParser.Parse(Ludra, "6a9c", new DateTimeOffset(2026, 10, 23, 0, 0, 0, TimeSpan.Zero), "Ludra Awakens on November 4th (Updated 10/30)");
        var c = Assert.Single(campaigns);
        Assert.Equal(new DateTimeOffset(2026, 11, 4, 14, 0, 0, TimeSpan.Zero), c.Start);       // 06:00 PST
        Assert.Equal(new DateTimeOffset(2026, 11, 18, 7, 59, 0, TimeSpan.Zero), c.End);
        Assert.Equal(3, c.Rewards.Count);
        Assert.Equal(840, c.Rewards[^1].Minutes);
        Assert.Equal("Ludra Awakens", c.Name);
        Assert.Null(claimBy); // "Claim your Odyle Energy" is a login reward, not a claim date
    }

    [Fact]
    public void A_notice_without_a_drops_table_has_no_campaign()
    {
        Assert.Empty(DropsParser.Parse("<p>Maintenance from 07:00 to 10:00</p>", "x", DateTimeOffset.UtcNow, "Maintenance").Campaigns);
    }
}
