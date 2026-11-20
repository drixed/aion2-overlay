using AionDpsMeter.Timers.Craft;

namespace AionDpsMeter.Timers.Tests.Craft;

public class CraftTests
{
    // a sword (900) from two ingots (800) and a gem (700); an ingot from three ores (600)
    private static readonly CraftBook Book = new(
        [
            new CraftRecipe(1, true, "Blacksmithing", 10, "Auto", 900, 1, [new CraftComponent(800, 2, []), new CraftComponent(700, 1, [])]),
            new CraftRecipe(2, true, "Blacksmithing", 1, "Combo", 800, 1, [new CraftComponent(600, 3, [])]),
        ],
        new Dictionary<int, (int, string)> { [900] = (3, "Меч"), [800] = (1, "Слиток"), [700] = (2, "Камень"), [600] = (1, "Руда") });

    [Fact]
    public void Intermediates_come_from_the_stock_first_and_the_rest_down_to_raw_materials()
    {
        var stock = new Dictionary<int, long> { [800] = 1, [600] = 1 };
        var result = Assert.Single(CraftCalc.Compute(Book, [new CraftEntry(1, 1)], stock, resolve: true, elyos: true));
        Assert.Equal([new CraftLine(800, 1, 1), new CraftLine(600, 3, 1), new CraftLine(700, 1, 0)], result.Materials);
        Assert.Equal([(800, 1L)], result.Intermediates);
        Assert.Equal(40, result.Percent);
        Assert.Equal([new CraftLine(600, 3, 1), new CraftLine(700, 1, 0), new CraftLine(800, 1, 1)], CraftCalc.Total([result]));
    }

    [Fact]
    public void A_bound_twin_of_a_material_counts_like_the_material()
    {
        // Nov 2026: "Воля ярости" showed 1 / 3 while the game showed 12 / 3 — it counts "Воля ярости (привяз.)", another item id, too
        var book = new CraftBook(
            [new CraftRecipe(1, true, "Alchemy", 1, "Auto", 900, 1, [new CraftComponent(610511014, 3, [(630511014, 3), (660511002, 3)])])],
            new Dictionary<int, (int, string)> { [900] = (4, "Гримуар"), [610511014] = (4, "Воля ярости"), [630511014] = (4, "Воля ярости (привяз.)") });
        var stock = new Dictionary<int, long> { [610511014] = 1, [630511014] = 11 };
        var result = Assert.Single(CraftCalc.Compute(book, [new CraftEntry(1, 5)], stock, resolve: true, elyos: true));
        Assert.Equal([new CraftLine(610511014, 15, 12)], result.Materials);
    }

    [Fact]
    public void Without_resolving_the_ingots_are_the_material()
    {
        var result = Assert.Single(CraftCalc.Compute(Book, [new CraftEntry(1, 2)], new Dictionary<int, long>(), resolve: false, elyos: true));
        Assert.Equal([new CraftLine(800, 4, 0), new CraftLine(700, 2, 0)], result.Materials);
    }

    [Fact]
    public void The_real_table_knows_recipes_and_russian_names()
    {
        var book = CraftBook.Default;
        Assert.True(book.Recipes.Count > 1000);
        var recipe = book.Recipes.First(r => r.Elyos && !r.Hidden);
        Assert.False(string.IsNullOrEmpty(book.Name(recipe.Product)));
    }

    private static byte[] Record(byte kind, int item, long quantity, byte container, byte slot) =>
        [kind, 0, 0, 0, 0, 0, 0, 0, 0, 0, .. BitConverter.GetBytes(item), .. BitConverter.GetBytes(quantity), container, slot, 0, 0, 0, 0];

    [Fact]
    public void The_login_list_gives_the_inventory_and_both_storages_and_changes_follow()
    {
        const int Ore = 610000600, Gem = 610000700, Sword = 110000900;            // item ids are nine digits: a smaller number is no anchor
        var stock = new CraftStock(i => i is Ore or Gem or Sword);
        byte[] login = [0x11, 0x56, .. new byte[10],
            .. Record(0, Ore, 5, 1, 0), .. Record(0, Ore, 7, 15, 3), .. Record(0, Gem, 2, 2, 9), .. Record(0, Sword, 1, 11, 1), .. new byte[5000]];
        stock.OnFrame(DateTime.Now, login);
        Assert.Equal(12, stock.Count(Ore));                         // inventory 5 + character storage 7
        Assert.Equal(2, stock.Count(Gem));                          // account storage
        Assert.Equal(0, stock.Count(Sword));                        // worn gear is no material
        stock.OnFrame(DateTime.Now, [0x1B, 0x56, .. new byte[10], .. Record(1, Ore, 2, 1, 0)]);
        Assert.Equal(9, stock.Count(Ore));
        stock.OnFrame(DateTime.Now, [0x1B, 0x56, .. new byte[10], .. Record(2, Ore, 0, 1, 0)]);
        Assert.Equal(7, stock.Count(Ore));
    }

    [Fact]
    public void The_plan_is_kept()
    {
        using var dir = new TempDir();
        var plan = new CraftPlan(dir.File("craft-plan.json"));
        plan.Set(1, 3);
        plan.Set(2, 1);
        plan.Set(2, 0);
        Assert.Equal([new CraftEntry(1, 3)], new CraftPlan(dir.File("craft-plan.json")).Entries);
    }
}
