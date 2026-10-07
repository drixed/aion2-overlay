using AionDpsMeter.Timers.Bosses;

namespace AionDpsMeter.Timers.Tests.Bosses;

public class FieldBossListParserTests
{
    private static DateTime Trim(long ms)
    {
        var t = DateTimeOffset.FromUnixTimeMilliseconds(ms).UtcDateTime;
        return new DateTime(t.Year, t.Month, t.Day, t.Hour, t.Minute, t.Second);
    }

    [Fact]
    public void Altgard_list_decodes_every_slot()
    {
        var list = FieldBossListParser.Parse(Fixtures.Bytes(Fixtures.AltgardList))!;
        Assert.Equal(1110, list.MapId);
        Assert.True(list.Complete);
        Assert.Equal(Enumerable.Range(111001, 24), list.Slots.Select(s => s.SlotId).Order());
        Assert.Equal([111001, 111009, 111012], list.Slots.Where(s => s.Alive).Select(s => s.SlotId));

        var danar = list.Slots.Single(s => s.SlotId == 111001);
        Assert.InRange(danar.X, -79656f, -79654f);
        Assert.Equal(new DateTime(2026, 10, 5, 21, 23, 12), Trim(danar.AtMs));
        Assert.Equal(new DateTime(2026, 10, 6, 1, 9, 4), Trim(list.Slots.Single(s => s.SlotId == 111021).AtMs));
        Assert.Equal(new DateTime(2026, 10, 5, 23, 29, 28), Trim(list.Slots.Single(s => s.SlotId == 111005).AtMs)); // extra byte
    }

    [Fact]
    public void Slots_without_a_time_decode()
    {
        var list = FieldBossListParser.Parse(Fixtures.Bytes(Fixtures.List20))!;
        Assert.True(list.Complete);
        Assert.Equal(20, list.MapId);
        Assert.Equal([2001, 2002, 2003, 2004, 2005, 2006, 2008, 2007], list.Slots.Select(s => s.SlotId));
        Assert.Equal(0, list.Slots[1].AtMs);
        Assert.Equal(new DateTimeOffset(2026, 10, 9, 19, 5, 0, TimeSpan.Zero), DateTimeOffset.FromUnixTimeMilliseconds(list.Slots[2].AtMs));
    }

    [Fact]
    public void The_last_slot_is_read_when_another_block_follows_the_list()
    {
        var list = FieldBossListParser.Parse(Fixtures.Bytes(Fixtures.AltgardLiveWithTrailer))!;
        Assert.True(list.Complete);
        Assert.Equal(Enumerable.Range(111001, 24), list.Slots.Select(s => s.SlotId).Order());
        Assert.Equal(1791387929829, list.Slots.Single(s => s.SlotId == 111024).AtMs);

        var map1010 = FieldBossListParser.Parse(Fixtures.Bytes(Fixtures.Map1010LiveWithTrailer))!;
        Assert.True(map1010.Complete);
        Assert.Equal(Enumerable.Range(101001, 24), map1010.Slots.Select(s => s.SlotId).Order());
    }

    [Fact]
    public void Truncated_list_returns_the_slots_read_so_far()
    {
        var bytes = Fixtures.Bytes(Fixtures.AltgardList);
        var list = FieldBossListParser.Parse(bytes.AsSpan(0, 60))!;
        Assert.False(list.Complete);
        Assert.Equal(24, list.Count);
        Assert.True(list.Slots.Count < 24);
    }

    [Fact]
    public void Garbage_never_throws()
    {
        Assert.Null(FieldBossListParser.Parse([1, 2, 3]));
        var random = new Random(42);
        for (var i = 0; i < 2000; i++)
        {
            var junk = new byte[random.Next(0, 300)];
            random.NextBytes(junk);
            _ = FieldBossListParser.Parse(junk); // must not throw
        }
    }

    [Fact]
    public void SameAs_compares_the_slots()
    {
        var a = FieldBossListParser.Parse(Fixtures.Bytes(Fixtures.AltgardList))!;
        var b = FieldBossListParser.Parse(Fixtures.Bytes(Fixtures.AltgardList))!;
        var c = FieldBossListParser.Parse(Fixtures.Bytes(Fixtures.List20))!;
        Assert.True(a.SameAs(b));
        Assert.False(a.SameAs(c));
    }

    [Fact]
    public void Varint_reads_leb128()
    {
        Assert.True(Wire.TryVarint([0x99, 0xE3, 0x06], 0, out var v, out var len));
        Assert.Equal(111001, v);
        Assert.Equal(3, len);
        Assert.False(Wire.TryVarint([0x80, 0x80], 0, out _, out _)); // unterminated
    }
}
