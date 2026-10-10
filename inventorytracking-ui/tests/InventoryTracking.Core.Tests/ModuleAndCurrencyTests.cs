using InventoryTracking.Core.Catalog;
using InventoryTracking.Core.Decoding;
using InventoryTracking.Core.Query;
using InventoryTracking.Core.Tests.Support;

namespace InventoryTracking.Core.Tests;

public class ModuleAndCurrencyTests
{
    private const long Mods = 32768;

    private static ExportCatalog Catalog(string storage, object[] rows, params (string Name, object? Value)[] headers)
    {
        var builder = new ExportFileBuilder().Storage(storage).Rows(rows);
        foreach (var (name, value) in headers)
        {
            builder.Header(name, value);
        }

        return Fixtures.Catalog(Fixtures.File($"itemdump_a_h_{storage}_1.json", builder.Build()));
    }

    [Theory]
    [InlineData("¬¬¬¬ Icemen's", "Icemen's", 4)]
    [InlineData("¬ Cloaking", "Cloaking", 1)]
    [InlineData("¬¬¬Durability", "Durability", 3)]
    [InlineData("Plasma Gun", "Plasma Gun", 0)]
    [InlineData("A ¬ in the middle", "A ¬ in the middle", 0)]
    [InlineData("", "", 0)]
    public void The_star_markers_in_a_name_become_a_star_count(string raw, string name, int stars)
    {
        Assert.Equal((name, stars), StarMarks.SplitName(raw));
    }

    [Fact]
    public void Descriptions_show_asterisks_instead_of_star_markers()
    {
        Assert.Equal("Faster reload *\nExplosive damage **\n", StarMarks.ToAsterisks("Faster reload ¬\nExplosive damage ¬¬\n"));
        Assert.Null(StarMarks.ToAsterisks(null));
    }

    [Fact]
    public void A_legendary_module_gets_its_stars_a_clean_name_and_its_description()
    {
        var catalog = Catalog("stash",
        [
            new { name = "¬¬¬ Durability", count = 2, id = 1, value = 5, filter = Mods, level = 1, weight = 0.5, card = "ok", desc = "Reduces item wear" },
            new { name = "¬ Cloaking", count = 1, id = 2, value = 5, filter = Mods, level = 1, weight = 0.5 },
            new { name = "Enclave Plasma Gun Short Recon Scope", count = 1, id = 3, value = 5, filter = Mods, level = 1, weight = 0.5 },
        ]);

        var durability = catalog.Items.Single(item => item.Name == "Durability");

        Assert.Equal(3, durability.Stars);
        Assert.False(durability.Legendary);
        Assert.Equal("Reduces item wear", Assert.Single(durability.Effects).Text);
        Assert.Contains("durability", durability.SearchText);
        Assert.DoesNotContain("¬", durability.SearchText);
        Assert.Equal(["Cloaking", "Durability"], catalog.Items.Where(item => item.Stars > 0).Select(item => item.Name).Order());
        Assert.Equal(0, catalog.Items.Single(item => item.Name.StartsWith("Enclave")).Stars);
    }

    [Fact]
    public void The_star_filter_finds_legendary_modules()
    {
        var catalog = Catalog("stash",
        [
            new { name = "¬¬¬ Durability", count = 1, id = 1, value = 5, filter = Mods },
            new { name = "¬ Cloaking", count = 1, id = 2, value = 5, filter = Mods },
            new { name = "Combat Armor", count = 1, id = 3, value = 5, filter = 8, legendary = true, stars = 3 },
        ]);

        var threeStars = ItemQueryEngine.Select(catalog, new ItemQuery { Stars = new HashSet<int> { 3 } })!.Select(item => item.Name);
        var modsOneStar = ItemQueryEngine.Select(catalog, new ItemQuery { Types = new HashSet<string> { "mods" }, Stars = new HashSet<int> { 1 } })!.Select(item => item.Name);

        Assert.Equal(["Durability", "Combat Armor"], threeStars);
        Assert.Equal(["Cloaking"], modsOneStar);
    }

    [Fact]
    public void A_legendary_items_stars_from_the_file_win_over_name_markers()
    {
        var catalog = Catalog("player", [new { name = "¬ Odd", count = 1, id = 1, value = 1, filter = 4, legendary = true, stars = 2 }]);

        Assert.Equal(2, Assert.Single(catalog.Items).Stars);
    }

    [Fact]
    public void Currencies_of_a_player_export_are_named_ordered_and_attached_to_the_group()
    {
        var catalog = Catalog("player", [new { name = "Thing", count = 1, id = 1, value = 1, filter = 4 }],
            ("currencies", new object[]
            {
                new { type = 11, amount = 5, max = 10000 },
                new { type = 0, amount = 11523, max = 40000 },
                new { type = 1, amount = 70, max = 11000 },
                new { type = 99, amount = 3 },
                new { type = 0, amount = 1 },
                new { type = "x", amount = 1 },
                new { amount = 1 },
            }));

        var currencies = Assert.Single(catalog.Groups).Currencies;

        Assert.Equal(["Caps", "Legendary Tokens", "Expedition Stamps", "Currency 99"], currencies.Select(c => c.Name));
        Assert.Equal([11523L, 70, 5, 3], currencies.Select(c => c.Amount));
        Assert.Equal(40000, currencies[0].Max);
        Assert.Null(currencies[3].Max);
    }

    [Fact]
    public void A_file_without_currencies_has_an_empty_list()
    {
        var catalog = Catalog("stash", [new { name = "Thing", count = 1, id = 1, value = 1, filter = 4 }]);

        Assert.Empty(Assert.Single(catalog.Groups).Currencies);
    }
}
