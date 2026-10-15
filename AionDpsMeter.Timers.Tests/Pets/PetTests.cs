using AionDpsMeter.Timers.Pets;

namespace AionDpsMeter.Timers.Tests.Pets;

/// <summary>
/// EU capture 2026-10-16, pet "Сторожевой пес драканов" (collection id 1014): lv1 15/25 → 25 → level 2 with 2 souls
/// carried over → 12/75 at the next login. Bodies are after the opcode.
/// </summary>
public class PetParserTests
{
    [Fact]
    public void A_soul_packet_names_the_pet_and_the_amount() =>
        Assert.Equal(new PetSoul(1014, 1), PetParser.ParseSoul(Convert.FromHexString("01F603000001000000")));

    [Fact]
    public void The_exact_count_comes_when_a_level_is_full() =>
        Assert.Equal(new PetSoul(1014, 25), PetParser.ParseExact(Convert.FromHexString("01F603000019000000")));

    [Fact]
    public void A_level_up_names_the_pet_and_its_new_level() =>
        // 14 90 at 01:33:14.942, together with "25 of 25" (0E 90); 5C 8D twelve seconds later is something else
        Assert.Equal(new PetLevel(1014, 2), PetParser.ParseLevelUp(Convert.FromHexString("00F603000002000000030A0000000000000000000000")));

    [Fact]
    public void The_login_list_gives_every_pet_s_level()
    {
        // header 07 04 00 00 BC 01, then (id, id, level) records, then other data
        var body = Convert.FromHexString("07040000BC01" + "F5030000F503000003000000" + "F6030000F603000001000000" + "0302AD01");
        Assert.Equal([new PetLevel(1013, 3), new PetLevel(1014, 1)], PetParser.ParseList(body));
    }

    [Fact]
    public void The_login_list_also_gives_the_souls_within_the_level()
    {
        // relog 2026-10-16 02:39: after the records a varint count, then (u32 pet, u32 souls) — "Дозорщик драканов"
        // (1184) had 11/25 and showed 13/25 after two more souls; a maxed pet (1013) has no pair
        var body = Convert.FromHexString("07040000BC01" + "F5030000F503000003000000" + "E9030000E903000002000000" + "A0040000A004000001000000"
            + "02" + "E903000039000000" + "A00400000B000000" + "0302AD01");
        Assert.Equal([new PetLevel(1013, 3), new PetLevel(1001, 2, 57), new PetLevel(1184, 1, 11)], PetParser.ParseList(body));
    }
}

public class PetBookTests
{
    private static readonly DateTime T0 = new(2026, 10, 16, 1, 31, 0);
    private const int Dog = 1014, DogMonster = 2_050_317;

    [Fact]
    public void The_session_the_user_played_ends_with_the_numbers_the_game_showed()
    {
        var book = new PetBook();
        book.OnList([new PetLevel(Dog, 1)]);                    // login: level only
        book.OnSoul(Dog, 1, DogMonster);
        Assert.Equal(new PetView(Dog, 1, null, 25, 1), book.ForMonster(DogMonster));   // souls unknown yet: +1 this session

        book.OnExact(Dog, 25);                                     // the game: 25 of 25
        book.OnLevelUp(new PetLevel(Dog, 2));                      // and level 2 right away
        for (var i = 0; i < 4; i++) book.OnSoul(Dog, 1, DogMonster);
        Assert.Equal(new PetView(Dog, 2, 4, 75, 5), book.ForMonster(DogMonster));   // the game: 4/75
        for (var i = 0; i < 8; i++) book.OnSoul(Dog, 1, DogMonster);
        book.OnList([new PetLevel(Dog, 2)]);                       // relog: same level, the count stays
        Assert.Equal(12, book.ForMonster(DogMonster)!.Souls);
    }

    [Fact]
    public void A_level_gained_while_the_meter_was_off_makes_the_count_unknown()
    {
        var book = new PetBook();
        book.OnList([new PetLevel(Dog, 1)]);
        book.OnExact(Dog, 20);
        book.OnSoul(Dog, 1, DogMonster);
        book.OnList([new PetLevel(Dog, 2)]);
        Assert.Null(book.ForMonster(DogMonster)!.Souls);
    }

    [Fact]
    public void The_count_from_the_login_list_replaces_what_the_meter_counted()
    {
        var book = new PetBook();
        book.OnList([new PetLevel(Dog, 2)]);
        book.OnExact(Dog, 35);
        book.OnSoul(Dog, 1, DogMonster);                           // 36, but souls were missed while the meter was blind
        book.OnList([new PetLevel(Dog, 2, 57)]);                   // relog: the game says 57
        book.OnSoul(Dog, 1, DogMonster);
        Assert.Equal(58, book.ForMonster(DogMonster)!.Souls);
    }

    [Fact]
    public void A_level_up_moves_its_own_pet_and_leaves_the_rest()
    {
        // 2026-10-16: "Дозорщик драканов" (1184) 23/25 at login, then level 2; the other pet must not move
        var book = new PetBook();
        book.OnList([new PetLevel(1184, 1, 23), new PetLevel(Dog, 1, 25)]);
        book.OnSoul(1184, 1, DogMonster);
        book.OnSoul(1184, 2, DogMonster);                          // 26: a double drop carries one over
        book.OnLevelUp(new PetLevel(1184, 2));
        Assert.Equal(new PetView(1184, 2, 1, 75, 3), book.ForMonster(DogMonster));
        Assert.Equal(1, book.All().Single(r => r.View.Pet == Dog).View.Level);
    }

    [Fact]
    public void A_level_up_of_a_pet_with_an_unknown_count_starts_it_at_zero()
    {
        var book = new PetBook();
        book.OnList([new PetLevel(1184, 1)]);
        book.OnLevelUp(new PetLevel(1184, 2));
        Assert.Equal(0, book.All().Single().View.Souls);
    }

    [Fact]
    public void A_maxed_pet_shows_no_line() 
    {
        var book = new PetBook();
        book.OnList([new PetLevel(Dog, 3)]);
        book.OnSoul(Dog, 1, DogMonster);
        Assert.Null(book.ForMonster(DogMonster));
    }

    [Fact]
    public void The_book_survives_a_restart()
    {
        var book = new PetBook();
        book.OnList([new PetLevel(Dog, 2)]);
        book.OnExact(Dog, 12);
        book.OnSoul(Dog, 1, DogMonster);
        var again = PetBook.FromSnapshot(book.Snapshot());
        Assert.Equal(new PetView(Dog, 2, 13, 75, 0), again.ForMonster(DogMonster));
    }
}

public class PetListTests
{
    /// <summary>The Pets tab: every pet, closest to its next level first; unknown counts after them, maxed last.</summary>
    [Fact]
    public void All_pets_are_listed_closest_to_their_next_level_first()
    {
        var book = new PetBook();
        book.OnList([new PetLevel(1001, 3), new PetLevel(1014, 2), new PetLevel(1017, 2), new PetLevel(1026, 1)]);
        book.OnExact(1014, 70);      // 5 to go
        book.OnExact(1026, 10);      // 15 to go
        book.OnSoul(1017, 1, 2100855);
        var all = book.All();
        Assert.Equal([1014, 1026, 1017, 1001], all.Select(p => p.View.Pet));
        Assert.Equal(2100855, all.Single(p => p.View.Pet == 1017).Monster);
        Assert.Null(all.Single(p => p.View.Pet == 1001).Monster);
    }
}
