using InventoryTracking.Core.Catalog;
using InventoryTracking.Core.Model;
using InventoryTracking.Core.Sources;
using InventoryTracking.Core.Tests.Support;
using InventoryTracking.Core.Types;

namespace InventoryTracking.Core.Tests;

public class GroupingTests
{
    private static SourceFile Part(string name, int part, int parts, string snapshot = "aaaa1111", int totalRows = 4, int rows = 2, string storage = "stash", DateTime? written = null)
    {
        var builder = new ExportFileBuilder().Storage(storage).Snapshot(snapshot).Part(part, parts).Header("rows", totalRows);
        builder.Rows([.. Enumerable.Range(1, rows).Select(i => ExportFileBuilder.Row($"P{part}-{i}", part * 100 + i))]);
        return Fixtures.File(name, builder.Build(), written);
    }

    [Fact]
    public void Parts_are_merged_in_part_order_even_when_files_arrive_out_of_order()
    {
        var catalog = Fixtures.Catalog(Part("itemdump_a_h_stash_2.json", 2, 2), Part("itemdump_a_h_stash_1.json", 1, 2));

        var group = Assert.Single(catalog.Groups);
        Assert.Equal(GroupStatus.Complete, group.Status);
        Assert.Equal(["P1-1", "P1-2", "P2-1", "P2-2"], group.Items.Select(i => i.Name));
        Assert.Equal([1, 2], group.PartsFound);
        Assert.Empty(group.MissingParts);
    }

    [Fact]
    public void A_missing_part_makes_the_group_incomplete_and_lists_the_part()
    {
        var catalog = Fixtures.Catalog(Part("itemdump_a_h_stash_1.json", 1, 3, totalRows: 6), Part("itemdump_a_h_stash_3.json", 3, 3, totalRows: 6));

        var group = Assert.Single(catalog.Groups);
        Assert.Equal(GroupStatus.Incomplete, group.Status);
        Assert.Equal([2], group.MissingParts);
        Assert.Equal(3, group.PartsExpected);
        Assert.Equal(4, group.Rows);
        Assert.Equal(6, group.ExpectedRows);
    }

    [Fact]
    public void A_row_count_that_differs_from_the_header_makes_the_group_incomplete()
    {
        var catalog = Fixtures.Catalog(Part("itemdump_a_h_stash_1.json", 1, 1, totalRows: 9));

        Assert.Equal(GroupStatus.Incomplete, Assert.Single(catalog.Groups).Status);
    }

    [Fact]
    public void Parts_with_different_snapshots_stay_separate_groups_marked_as_mixed()
    {
        var catalog = Fixtures.Catalog(
            Part("itemdump_a_h_stash_1.json", 1, 2, snapshot: "aaaa1111"),
            Part("itemdump_a_h_stash_2.json", 2, 2, snapshot: "bbbb2222"),
            Part("itemdump_a_h_player_1.json", 1, 1, storage: "player", totalRows: 2));

        var stash = catalog.Groups.Where(g => g.Storage == "stash").ToList();
        Assert.Equal(2, stash.Count);
        Assert.All(stash, g => Assert.Equal(GroupStatus.MixedSnapshots, g.Status));
        Assert.All(stash, g => Assert.NotEmpty(g.MissingParts));
        Assert.Equal(2, stash.Select(g => g.Id).Distinct().Count());
        Assert.Equal(GroupStatus.Complete, catalog.Groups.Single(g => g.Storage == "player").Status);
    }

    [Fact]
    public void A_duplicate_part_is_ignored_and_reported()
    {
        var catalog = Fixtures.Catalog(
            Part("itemdump_a_h_stash_1.json", 1, 1, totalRows: 2),
            Part("itemdump_a_h_stash_1b.json", 1, 1, totalRows: 2));

        Assert.Equal(2, Assert.Single(catalog.Groups).Rows);
        Assert.Equal(IssueKind.DuplicatePart, Assert.Single(catalog.Issues).Kind);
    }

    [Fact]
    public void A_damaged_file_is_reported_with_its_name_and_does_not_block_the_others()
    {
        var catalog = Fixtures.Catalog(
            Fixtures.File("itemdump_a_h_bad_1.json", "{ broken"u8.ToArray()),
            Part("itemdump_a_h_stash_1.json", 1, 1, totalRows: 2),
            new SourceFile("itemdump_a_h_locked_1.json", DateTime.UtcNow, null, "The file cannot be read: locked"));

        Assert.Single(catalog.Groups);
        Assert.Equal(["itemdump_a_h_bad_1.json", "itemdump_a_h_locked_1.json"], catalog.Issues.Select(i => i.File));
        Assert.Equal([IssueKind.Invalid, IssueKind.Unreadable], catalog.Issues.Select(i => i.Kind));
        Assert.Equal(3, catalog.FileCount);
    }

    [Fact]
    public void An_empty_list_is_a_complete_group_without_items()
    {
        var group = Assert.Single(Fixtures.Catalog(Part("itemdump_a_h_stash_1.json", 1, 1, totalRows: 0, rows: 0)).Groups);

        Assert.Equal(GroupStatus.Complete, group.Status);
        Assert.Empty(group.Items);
    }

    [Fact]
    public void Items_get_stable_keys_group_context_and_types()
    {
        var group = Assert.Single(Fixtures.Catalog(Part("itemdump_a_h_stash_1.json", 1, 1, totalRows: 2)).Groups);

        Assert.Equal([$"{group.Id}.0", $"{group.Id}.1"], group.Items.Select(i => i.Key));
        Assert.All(group.Items, item => Assert.Equal("stash", item.Storage));
        Assert.Equal(["weapon"], group.Items[0].Types);
    }

    [Fact]
    public void Capture_time_is_the_newest_file_time_of_the_group()
    {
        var older = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var newer = older.AddDays(3);

        var group = Assert.Single(Fixtures.Catalog(
            Part("itemdump_a_h_stash_1.json", 1, 2, written: older),
            Part("itemdump_a_h_stash_2.json", 2, 2, written: newer)).Groups);

        Assert.Equal(newer, group.CapturedAtUtc);
    }

    [Fact]
    public void Totals_use_count_times_weight_and_count_times_value()
    {
        var file = Fixtures.File(
            "itemdump_a_h_player_1.json",
            new ExportFileBuilder().Storage("player").Rows(
                ExportFileBuilder.Row("A", 1, count: 10, weight: 0.5, value: 3),
                ExportFileBuilder.Row("B", 2, count: 2, weight: 2.25, value: 100)).Build());

        var totals = Assert.Single(Fixtures.Catalog(file).Groups).Totals;

        Assert.Equal(new ItemTotals(2, 12, 9.5, 0, 230), totals);
    }

    [Fact]
    public void Characters_are_listed_with_storages_in_game_order()
    {
        var catalog = Fixtures.Catalog(
            Part("itemdump_a_h_aid_1.json", 1, 1, storage: "aid", totalRows: 2),
            Part("itemdump_a_h_stash_1.json", 1, 1, totalRows: 2),
            Part("itemdump_a_h_player_1.json", 1, 1, storage: "player", totalRows: 2));

        var character = Assert.Single(CharacterOverview.Build(catalog));

        Assert.Equal(["player", "stash", "aid"], character.Storages.Select(g => g.Storage));
    }

    [Fact]
    public void The_real_exports_form_one_character_with_five_storages()
    {
        var character = Assert.Single(CharacterOverview.Build(Fixtures.RealCatalog()));

        Assert.Equal(["player", "stash", "scrap", "ammo", "aid"], character.Storages.Select(g => g.Storage));
    }
}
