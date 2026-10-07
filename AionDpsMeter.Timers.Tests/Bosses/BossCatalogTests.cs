using AionDpsMeter.Timers.Bosses;

namespace AionDpsMeter.Timers.Tests.Bosses;

public class BossCatalogTests
{
    private const string Ru = """
        { "2400017": { "name": "Данар", "isBoss": true },
          "2400800": { "name": "Гартуа", "isBoss": true },
          "2400500": { "name": "Чучело", "isBoss": true, "isDummy": true },
          "2000002": { "name": "Драконид", "isBoss": false } }
        """;

    private const string Rat = """
        { "2400017": { "name": "Danar", "isBoss": true },
          "2400999": { "name": "New boss in an old block", "isBoss": true },
          "2600001": { "name": "Fresh boss", "isBoss": true } }
        """;

    [Fact]
    public void Knows_bosses_and_names()
    {
        var c = BossCatalog.FromJson(Ru);
        Assert.True(c.IsBoss(2400017));
        Assert.False(c.IsBoss(2400500)); // training dummy
        Assert.False(c.IsBoss(2000002));
        Assert.Equal("Данар", c.Name(2400017));
        Assert.Equal("Босс 123", c.Name(123));
    }

    [Fact]
    public void First_table_wins_names_and_later_tables_add_new_codes()
    {
        var c = BossCatalog.FromJson(Ru, Rat);
        Assert.Equal("Данар", c.Name(2400017));
        Assert.True(c.IsBoss(2600001));
        Assert.Equal("Fresh boss", c.Name(2600001));
    }

    [Fact]
    public void A_block_is_counted_from_the_first_table_that_has_bosses_in_it()
    {
        var c = BossCatalog.FromJson(Ru, Rat);
        Assert.Equal([2400017, 2400800], c.FieldBossesInBlock(2400));
        Assert.Equal([2600001], c.FieldBossesInBlock(2600));
    }

    [Fact]
    public void BossInSlot_needs_the_block_size_to_match_the_list()
    {
        var c = BossCatalog.FromJson(Ru);
        Assert.Equal(2400800, c.BossInSlot(2400, 1110, 111002, 2));
        Assert.Equal(0, c.BossInSlot(2400, 1110, 111002, 3));
        Assert.Equal(0, c.BossInSlot(2400, 1110, 111003, 2));
    }

    [Fact]
    public void A_broken_table_is_skipped()
    {
        var c = BossCatalog.FromJson("{ broken", Ru);
        Assert.True(c.IsBoss(2400017));
    }

    [Fact]
    public void Shipped_table_resolves_the_Altgard_list()
    {
        var c = BossCatalog.FromJson(EmbeddedResources.Read("npcs.ru.json"));
        Assert.Equal(24, c.FieldBossesInBlock(2400).Count);
        Assert.Equal(2400800, c.BossInSlot(2400, 1110, 111021, 24));
        Assert.Equal(2400017, c.BossInSlot(2400, 1110, 111001, 24));
        Assert.NotEqual("Босс 2400800", c.Name(2400800));
    }
}
