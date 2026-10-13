using AionDpsMeter.Timers.Overlay;

namespace AionDpsMeter.Timers.Tests.Overlay;

public class RowOrderTests
{
    private static readonly string[] ByDamage = ["Wolves", "Stoopzz", "Me", "Castform"];

    private static IReadOnlyList<(string Name, int Rank)> Order(bool meOnTop, bool reverse) =>
        RowOrder.Apply(ByDamage, n => n == "Me", meOnTop, reverse);

    [Fact]
    public void By_default_the_order_stays_and_ranks_follow_damage() =>
        Assert.Equal([("Wolves", 1), ("Stoopzz", 2), ("Me", 3), ("Castform", 4)], Order(false, false));

    [Fact]
    public void Keep_me_on_top_moves_my_row_up_but_keeps_my_rank() =>
        Assert.Equal([("Me", 3), ("Wolves", 1), ("Stoopzz", 2), ("Castform", 4)], Order(true, false));

    [Fact]
    public void First_place_at_the_bottom_reverses_the_list() =>
        Assert.Equal([("Castform", 4), ("Me", 3), ("Stoopzz", 2), ("Wolves", 1)], Order(false, true));

    [Fact]
    public void With_both_my_row_sits_next_to_the_header_side_it_belongs_to() =>
        Assert.Equal([("Castform", 4), ("Stoopzz", 2), ("Wolves", 1), ("Me", 3)], Order(true, true));
}
