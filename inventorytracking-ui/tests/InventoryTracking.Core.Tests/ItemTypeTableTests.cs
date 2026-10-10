using InventoryTracking.Core.Types;

namespace InventoryTracking.Core.Tests;

public class ItemTypeTableTests
{
    private static readonly ItemTypeTable Table = ItemTypeTable.Default;

    [Theory]
    [InlineData(4L, "weapon")]
    [InlineData(8L, "armor")]
    [InlineData(16L, "apparel")]
    [InlineData(32L, "food")]
    [InlineData(64L, "aid")]
    [InlineData(1024L, "notes")]
    [InlineData(2048L, "notes")]
    [InlineData(4096L, "misc")]
    [InlineData(8192L, "misc")]
    [InlineData(16384L, "junk")]
    [InlineData(262144L, "junk")]
    [InlineData(32768L, "mods")]
    [InlineData(65536L, "ammo")]
    [InlineData(131072L, "holotapes")]
    public void Each_bit_maps_to_its_type(long filter, string expected) =>
        Assert.Equal([expected], Table.Classify(filter));

    [Fact]
    public void A_mask_with_several_bits_belongs_to_several_types() =>
        Assert.Equal(["food", "aid"], Table.Classify(32 | 64));

    [Theory]
    [InlineData(5L, new[] { "weapon", "other" })]
    [InlineData(540672L, new[] { "junk", "other" })]
    [InlineData(1L, new[] { "other" })]
    [InlineData(0L, new[] { "other" })]
    public void Unknown_bits_add_the_other_type(long filter, string[] expected) =>
        Assert.Equal(expected, Table.Classify(filter));

    [Fact]
    public void The_table_is_data_driven_and_ends_with_other()
    {
        var custom = new ItemTypeTable([new ItemTypeDefinition("thing", "Thing", [2])]);

        Assert.Equal(["thing"], custom.Classify(2));
        Assert.Equal(["thing", "other"], custom.Types.Select(t => t.Id));
        Assert.Equal(ItemTypeTable.OtherId, Table.Types[^1].Id);
    }
}
