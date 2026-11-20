using AionDpsMeter.Timers.Farm;

namespace AionDpsMeter.Timers.Tests.Farm;

public class FarmIncomeTests
{
    private static readonly DateTime T0 = new(2026, 11, 20, 4, 0, 0);

    // One inventory record (body offsets): kind byte 10 before the item, item id u32, quantity u64, container, slot.
    private static byte[] Record(byte kind, int item, long quantity, byte container, byte slot)
    {
        var r = new byte[10 + 14 + 4];
        r[0] = kind;
        BitConverter.GetBytes(item).CopyTo(r, 10);
        BitConverter.GetBytes(quantity).CopyTo(r, 14);
        r[22] = container;
        r[23] = slot;
        return r;
    }

    private static byte[] Frame(params byte[][] records) => [0x1b, 0x56, .. records.SelectMany(r => r)];

    [Fact]
    public void Records_are_read_with_item_quantity_container_and_slot()
    {
        var records = InventoryRecords.Read(Frame(Record(1, FarmIncome.Kina, 1_250_000, 1, 3), Record(1, FarmIncome.AbyssPoints, 9_000, 1, 4)));
        Assert.Equal([(FarmIncome.Kina, 1_250_000L, 1, 3), (FarmIncome.AbyssPoints, 9_000L, 1, 4)], records.Select(r => (r.Item, r.Quantity, r.Container, r.Slot)));
    }

    [Fact]
    public void Kina_and_abyss_points_count_what_came_in_per_hour()
    {
        var f = new FarmIncome();
        f.OnChange(InventoryRecords.Read(Frame(Record(1, FarmIncome.Kina, 1_000_000, 1, 3))), T0);           // first sight: the base
        f.OnChange(InventoryRecords.Read(Frame(Record(1, FarmIncome.Kina, 1_012_000, 1, 3))), T0.AddMinutes(30));
        f.OnChange(InventoryRecords.Read(Frame(Record(1, FarmIncome.Kina, 1_005_000, 1, 3))), T0.AddMinutes(40)); // spent: not income
        f.OnChange(InventoryRecords.Read(Frame(Record(1, FarmIncome.AbyssPoints, 500, 1, 4))), T0.AddMinutes(45));
        f.OnChange(InventoryRecords.Read(Frame(Record(1, FarmIncome.AbyssPoints, 666, 1, 4))), T0.AddMinutes(60));

        Assert.Equal(12_000, f.Gained(FarmIncome.Kina));
        Assert.Equal(166, f.Gained(FarmIncome.AbyssPoints));
        Assert.Equal(12_000, f.PerHour(FarmIncome.Kina, T0.AddMinutes(60))!.Value, 1);
    }

    [Fact]
    public void A_big_drop_soon_after_the_start_gives_no_rate_per_hour_yet()
    {
        // Nov 2026: a boss chest worth 960 000 kina seven minutes after the start showed "5 710 757/ч"
        var f = new FarmIncome();
        f.OnChange(InventoryRecords.Read(Frame(Record(1, FarmIncome.Kina, 1_000_000, 1, 3))), T0);
        f.OnChange(InventoryRecords.Read(Frame(Record(1, FarmIncome.Kina, 1_960_000, 1, 3))), T0.AddMinutes(7));
        Assert.Equal(960_000, f.Gained(FarmIncome.Kina));
        Assert.Null(f.PerHour(FarmIncome.Kina, T0.AddMinutes(10)));
        Assert.Equal(960_000 / 0.5, f.PerHour(FarmIncome.Kina, T0.AddMinutes(30))!.Value, 1);
    }

    [Fact]
    public void A_resent_inventory_after_a_sort_is_no_income()
    {
        var f = new FarmIncome();
        f.OnSnapshot(InventoryRecords.Read(Frame(Record(1, FarmIncome.Kina, 1_000_000, 1, 3))), T0);
        var sort = Enumerable.Range(0, 12).Select(i => Record(1, FarmIncome.Kina, 1_000_000, 1, (byte)(10 + i))).ToArray();
        f.OnChange(InventoryRecords.Read(Frame(sort)), T0.AddMinutes(5));
        Assert.Equal(0, f.Gained(FarmIncome.Kina));
    }

    [Fact]
    public void Reset_starts_counting_again()
    {
        var f = new FarmIncome();
        f.OnChange(InventoryRecords.Read(Frame(Record(1, FarmIncome.Kina, 100, 1, 3))), T0);
        f.OnChange(InventoryRecords.Read(Frame(Record(1, FarmIncome.Kina, 300, 1, 3))), T0.AddMinutes(1));
        f.Reset(T0.AddMinutes(2));
        Assert.Equal(0, f.Gained(FarmIncome.Kina));
        f.OnChange(InventoryRecords.Read(Frame(Record(1, FarmIncome.Kina, 350, 1, 3))), T0.AddMinutes(3));
        Assert.Equal(50, f.Gained(FarmIncome.Kina));
    }
}
