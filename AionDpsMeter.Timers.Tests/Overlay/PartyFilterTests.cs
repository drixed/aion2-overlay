using AionDpsMeter.Timers.Overlay;

namespace AionDpsMeter.Timers.Tests.Overlay;

public class PartyFilterTests
{
    private static readonly PartyRow Me = new(1, IsUser: true, CharacterLevel: 0, Damage: 6_000);
    private static readonly PartyRow Friend = new(2, IsUser: false, CharacterLevel: 45, Damage: 4_000);
    private static readonly PartyRow Stranger = new(3, IsUser: false, CharacterLevel: 0, Damage: 10_000);

    [Fact]
    public void Party_members_are_me_and_players_known_from_the_party_packet()
    {
        // Upstream fills CharacterLevel only from the party packet; strangers nearby never have it.
        Assert.True(PartyFilter.IsMember(Me));
        Assert.True(PartyFilter.IsMember(Friend));
        Assert.False(PartyFilter.IsMember(Stranger));
    }

    [Fact]
    public void Shares_are_recomputed_within_the_party()
    {
        var shares = PartyFilter.Shares([Me, Friend, Stranger]);
        Assert.Equal(2, shares.Count);
        Assert.Equal(60, shares[1], 3);
        Assert.Equal(40, shares[2], 3);
        Assert.Equal(100, PartyFilter.Shares([Me, Stranger])[1], 3); // solo: just me
        Assert.Empty(PartyFilter.Shares([Stranger]));
    }

    [Fact]
    public void The_top_member_fills_the_bar()
    {
        var bars = PartyFilter.Bars([Me, Friend, Stranger]);
        Assert.Equal(100, bars[1], 3);
        Assert.Equal(66.667, bars[2], 3);
    }
}
