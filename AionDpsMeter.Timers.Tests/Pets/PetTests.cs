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
    public void A_level_up_carries_the_new_level() =>
        Assert.Equal(2, PetParser.ParseLevelUp(Convert.FromHexString("01E15FCC0102000000010546000000")));

    [Fact]
    public void The_login_list_gives_every_pet_s_level()
    {
        // header 07 04 00 00 BC 01, then (id, id, level) records, then other data
        var body = Convert.FromHexString("07040000BC01" + "F5030000F503000003000000" + "F6030000F603000001000000" + "0302AD01");
        Assert.Equal([new PetLevel(1013, 3), new PetLevel(1014, 1)], PetParser.ParseList(body));
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
        book.OnSoul(Dog, 1, DogMonster);
        book.OnSoul(Dog, 1, DogMonster);                           // 27
        Assert.Equal(Dog, book.OnLevelUp(2));                      // level 2, 2 carried over
        Assert.Equal(new PetView(Dog, 2, 2, 75, 3), book.ForMonster(DogMonster));
        for (var i = 0; i < 10; i++) book.OnSoul(Dog, 1, DogMonster);
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
