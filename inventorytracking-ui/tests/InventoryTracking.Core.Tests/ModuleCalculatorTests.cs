using InventoryTracking.Core.Modules;
using InventoryTracking.Core.Tests.Support;

namespace InventoryTracking.Core.Tests;

public class ModuleCalculatorTests
{
    private const long Mods = 32768;

    private static InventoryTracking.Core.Catalog.ExportCatalog Catalog(params object[] rows) =>
        Fixtures.Catalog(Fixtures.File("itemdump_a_h_stash_1.json", new ExportFileBuilder().Storage("stash").Rows(rows).Build()));

    private static object Mod(string name, int count, int id, object? vending = null) =>
        new { name, count, id, value = 5, filter = Mods, level = 1, weight = 0.1 };

    [Theory]
    [InlineData(1, 3)]
    [InlineData(2, 6)]
    [InlineData(3, 12)]
    [InlineData(4, 24)]
    [InlineData(5, 48)]
    [InlineData(0, 0)]
    public void A_mod_is_worth_three_times_two_to_the_stars_minus_one_modules(int stars, long modules) =>
        Assert.Equal(modules, ModuleCalculator.ModulesEach(stars));

    [Fact]
    public void Names_match_without_case_punctuation_or_plural()
    {
        Assert.Equal(ModuleCalculator.NameKey("Pin-Pointer's"), ModuleCalculator.NameKey("pinpointers"));
        Assert.NotEqual(ModuleCalculator.NameKey("Strength"), ModuleCalculator.NameKey("Stealth"));
    }

    [Fact]
    public void Counts_junk_per_tier_with_leaders_and_combines_identical_rows()
    {
        var catalog = Catalog(
            Mod("¬ Chameleon", 10, 1), Mod("¬ Chameleon", 6, 2),
            Mod("¬¬ Steady", 33, 3), Mod("¬¬¬¬ Viper's", 39, 4),
            Mod("¬¬¬¬ Two Shot", 1, 5), Mod("Enclave Scope", 4, 6));

        var summary = ModuleCalculator.Calculate(catalog, new ModuleSettings(null, [new ValuableMod("two shot", null)], 0.3m))!;

        Assert.Equal([1, 2, 4], summary.Tiers.Select(t => t.Stars));
        var chameleon = Assert.Single(summary.Tiers[0].Rows);
        Assert.Equal((16L, 48L, 14.4m), (chameleon.Count, chameleon.Modules, chameleon.Leaders));
        Assert.Equal(198, summary.Tiers[1].Modules);
        Assert.Equal(39 * 24, summary.Tiers[2].Modules);
        Assert.Equal(16 + 33 + 39, summary.Count);
        Assert.Equal(48 + 198 + 936, summary.Modules);
        Assert.Equal(1182 * 0.3m, summary.Leaders);
        Assert.False(chameleon.Valuable);
        var listed = summary.Tiers[2].Rows.Single(r => r.Valuable);
        Assert.Equal((4, 1L), (listed.Stars, listed.Count));
        Assert.Equal(1, summary.ValuableCount);
        Assert.Equal(1, summary.Tiers[2].ValuableCount);
    }

    [Fact]
    public void A_valuable_entry_with_stars_only_excludes_that_tier()
    {
        var catalog = Catalog(Mod("¬ Furious", 2, 1), Mod("¬¬¬¬ Furious", 3, 2));

        var summary = ModuleCalculator.Calculate(catalog, new ModuleSettings(null, [new ValuableMod("Furious", 4)], 2.5m))!;

        Assert.Equal([1, 4], summary.Tiers.Select(t => t.Stars));
        Assert.Equal(6 * 2.5m, summary.Leaders);
        Assert.Equal(3, summary.Tiers[1].ValuableCount);
        Assert.Equal(0, summary.Tiers[1].Count);
    }

    [Fact]
    public void An_unknown_group_gives_null() =>
        Assert.Null(ModuleCalculator.Calculate(Catalog(Mod("¬ A", 1, 1)), new ModuleSettings("nope", [], 0.3m)));
}

