using System.Text;
using InventoryTracking.Core.Catalog;
using InventoryTracking.Core.Export;
using InventoryTracking.Core.Query;
using InventoryTracking.Core.Tests.Support;

namespace InventoryTracking.Core.Tests;

public class VendorTests
{
    private static ExportCatalog Stash() => Fixtures.Catalog(Fixtures.File(
        "itemdump_a_h_stash_1.json",
        new ExportFileBuilder().Storage("stash").Rows(
            new { name = "Plasma Gun", count = 1, id = 1, value = 100, filter = 4, vending = new { machine = 1, price = 250, other = false } },
            new { name = "Stimpak", count = 12, id = 2, value = 5, filter = 64, vending = new { machine = 1, price = 20, other = false } },
            new { name = "Fusion Core", count = 3, id = 3, value = 50, filter = 4096, vending = new { machine = 1, price = (int?)null, other = false } },
            new { name = "Trophy", count = 1, id = 4, value = 1, filter = 4096, vending = new { machine = 2, price = (int?)null, other = false } },
            new { name = "Junk", count = 9, id = 5, value = 1, filter = 16384 }).Build()));

    private static IEnumerable<string> Names(ItemQuery query) => ItemQueryEngine.Select(Stash(), query)!.Select(item => item.Name);

    [Fact]
    public void Only_items_at_the_vending_machine_are_vendor_items_and_carry_their_price()
    {
        var items = Stash().Items.ToDictionary(item => item.Name);

        Assert.True(items["Plasma Gun"].Vendor);
        Assert.Equal(250, items["Plasma Gun"].Price);
        Assert.Equal(20, items["Stimpak"].Price);
        Assert.True(items["Fusion Core"].Vendor);
        Assert.Null(items["Fusion Core"].Price);
        Assert.False(items["Trophy"].Vendor);
        Assert.Null(items["Trophy"].Price);
        Assert.Equal(2, items["Trophy"].Vending!.Machine);
        Assert.False(items["Junk"].Vendor);
        Assert.Null(items["Junk"].Vending);
    }

    [Fact]
    public void The_vendor_filter_keeps_vendor_items_in_the_normal_list_and_can_isolate_them()
    {
        Assert.Equal(5, Names(new ItemQuery()).Count());
        Assert.Equal(["Plasma Gun", "Stimpak", "Fusion Core"], Names(new ItemQuery { Vendor = true }));
        Assert.Equal(["Trophy", "Junk"], Names(new ItemQuery { Vendor = false }));
        Assert.Equal(["Stimpak"], Names(new ItemQuery { Vendor = true, Types = new HashSet<string> { "aid" } }));
    }

    [Fact]
    public void Items_sort_by_listed_price_with_unlisted_ones_last_when_descending_reversed()
    {
        var descending = ItemQueryEngine.Select(Stash(), new ItemQuery { Sort = ItemSort.Parse("-price") })!.Select(item => item.Name).ToList();
        var ascending = ItemQueryEngine.Select(Stash(), new ItemQuery { Sort = ItemSort.Parse("price") })!.Select(item => item.Name).ToList();

        Assert.Equal(["Plasma Gun", "Stimpak"], descending.Take(2));
        Assert.Equal(["Stimpak", "Plasma Gun"], ascending.Skip(3));
    }

    [Fact]
    public void The_csv_export_has_vendor_and_price_columns()
    {
        var text = Encoding.UTF8.GetString(CsvExporter.Write(Stash().Items));
        var lines = text.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

        Assert.Contains(",vendor,price,", lines[0]);
        Assert.Contains("Plasma Gun", lines[1]);
        Assert.Contains(",yes,250,", lines[1]);
        Assert.Contains(",no,,", lines[^1]);
    }
}
