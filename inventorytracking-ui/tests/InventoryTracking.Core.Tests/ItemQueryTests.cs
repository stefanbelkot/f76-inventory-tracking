using InventoryTracking.Core.Catalog;
using InventoryTracking.Core.Query;
using InventoryTracking.Core.Tests.Support;

namespace InventoryTracking.Core.Tests;

public class ItemQueryTests
{
    private static readonly ExportCatalog Real = Fixtures.RealCatalog();

    private static ExportCatalog Small()
    {
        object[] rows =
        [
            new { name = "Plasma Gun", count = 1, id = 1, value = 100, filter = 4, level = 20, weight = 5.0, legendary = true, stars = 2, favorite = true, equipState = 1, desc = "Faster reload ¬\nExplosive damage ¬¬\n" },
            new { name = "Combat Armor", count = 1, id = 2, value = 40, filter = 8, level = 30, weight = 12.0, legendary = true, stars = 3, quest = true, desc = "Explosive resistance ¬\n" },
            new { name = ".Aluminum Scrap", count = 40, id = 3, value = 1, filter = 16384, level = 1, weight = 0.1 },
            new { name = "Purified Water", count = 5, id = 4, value = 2, filter = 32 | 64, level = 1, weight = 1.0 },
        ];
        return Fixtures.Catalog(Fixtures.File("itemdump_a_h_player_1.json", new ExportFileBuilder().Storage("player").Rows(rows).Build()));
    }

    private static IEnumerable<string> Names(ExportCatalog catalog, ItemQuery query) =>
        ItemQueryEngine.Select(catalog, query)!.Select(i => i.Name);

    [Fact]
    public void No_filter_returns_every_item() => Assert.Equal(4, ItemQueryEngine.Select(Small(), new ItemQuery())!.Count);

    [Fact]
    public void Type_filter_matches_any_of_the_selected_types_including_multi_type_items()
    {
        Assert.Equal(["Plasma Gun"], Names(Small(), new ItemQuery { Types = new HashSet<string> { "weapon" } }));
        Assert.Equal(["Purified Water"], Names(Small(), new ItemQuery { Types = new HashSet<string> { "aid" } }));
        Assert.Equal(["Plasma Gun", "Purified Water"], Names(Small(), new ItemQuery { Types = new HashSet<string> { "weapon", "food" } }));
    }

    [Fact]
    public void Text_search_needs_every_word_and_ignores_case()
    {
        Assert.Equal(["Plasma Gun"], Names(Small(), new ItemQuery { Text = "GUN plasma" }));
        Assert.Empty(Names(Small(), new ItemQuery { Text = "plasma armor" }));
    }

    [Fact]
    public void Text_search_covers_legendary_effect_text()
    {
        Assert.Equal(["Plasma Gun", "Combat Armor"], Names(Small(), new ItemQuery { Text = "explosive" }));
        Assert.Equal(["Plasma Gun"], Names(Small(), new ItemQuery { Text = "explosive reload" }));
    }

    [Fact]
    public void Flag_filters_restrict_the_result()
    {
        Assert.Equal(["Plasma Gun", "Combat Armor"], Names(Small(), new ItemQuery { Legendary = true }));
        Assert.Equal(["Plasma Gun"], Names(Small(), new ItemQuery { Favorite = true }));
        Assert.Equal(["Plasma Gun"], Names(Small(), new ItemQuery { Equipped = true }));
        Assert.Equal(["Combat Armor"], Names(Small(), new ItemQuery { Quest = true }));
        Assert.Equal(["Combat Armor"], Names(Small(), new ItemQuery { Stars = new HashSet<int> { 3 } }));
        Assert.Equal(2, Names(Small(), new ItemQuery { Legendary = false, Favorite = false }).Count());
    }

    [Fact]
    public void Sorting_works_in_both_directions_and_is_stable()
    {
        Assert.Equal([".Aluminum Scrap", "Combat Armor", "Plasma Gun", "Purified Water"], Names(Small(), new ItemQuery { Sort = ItemSort.Parse("name") }));
        Assert.Equal(["Combat Armor", "Plasma Gun"], Names(Small(), new ItemQuery { Sort = ItemSort.Parse("-weight"), Legendary = true }));
        Assert.Equal([".Aluminum Scrap", "Purified Water"], Names(Small(), new ItemQuery { Sort = ItemSort.Parse("-count"), Legendary = false }));
        Assert.Equal(["Plasma Gun", "Combat Armor", "Purified Water", ".Aluminum Scrap"], Names(Small(), new ItemQuery { Sort = ItemSort.Parse("-value") }));
    }

    [Fact]
    public void An_unknown_sort_column_is_rejected() => Assert.Throws<FormatException>(() => ItemSort.Parse("nope"));

    [Fact]
    public void Paging_slices_the_sorted_result_and_totals_cover_the_whole_filtered_list()
    {
        var result = ItemQueryEngine.Run(Real, new ItemQuery { Sort = ItemSort.Parse("name"), Page = 2, PageSize = 50 })!;

        Assert.Equal(50, result.Items.Count);
        Assert.Equal(Real.Items.Count, result.TotalRows);
        Assert.Equal(Real.Items.Count, result.Totals.Rows);
        Assert.Equal(Real.Items.Sum(i => i.Count), result.Totals.TotalCount);
        var all = ItemQueryEngine.Select(Real, new ItemQuery { Sort = ItemSort.Parse("name") })!;
        Assert.Equal(all.Skip(50).Take(50).Select(i => i.Key), result.Items.Select(i => i.Key));
    }

    [Fact]
    public void Page_size_is_clamped_and_a_page_past_the_end_is_empty()
    {
        Assert.Equal(ItemQuery.MaxPageSize, ItemQueryEngine.Run(Real, new ItemQuery { PageSize = 100000 })!.PageSize);
        Assert.Empty(ItemQueryEngine.Run(Real, new ItemQuery { Page = 999 })!.Items);
    }

    [Fact]
    public void A_group_filter_restricts_to_one_group_and_an_unknown_group_gives_null()
    {
        var stash = Real.Groups.Single(g => g.Storage == "stash");

        Assert.Equal(stash.Rows, ItemQueryEngine.Run(Real, new ItemQuery { GroupId = stash.Id })!.TotalRows);
        Assert.Null(ItemQueryEngine.Run(Real, new ItemQuery { GroupId = "missing" }));
    }

    [Fact]
    public void Legendary_filter_on_the_real_data_equals_the_legendary_flag_count()
    {
        var result = ItemQueryEngine.Run(Real, new ItemQuery { Legendary = true })!;

        Assert.Equal(Real.Items.Count(i => i.Legendary), result.TotalRows);
        Assert.Equal(result.TotalRows, result.Totals.LegendaryCount);
        Assert.NotEqual(0, result.TotalRows);
    }

    [Fact]
    public void Location_search_counts_rows_and_stacks_per_storage()
    {
        var result = LocationSearch.Run(Small(), "scrap");

        var hit = Assert.Single(result.Locations);
        Assert.Equal((1, 40L), (hit.Rows, hit.TotalCount));
        Assert.Equal(1, result.Rows);
        Assert.Empty(LocationSearch.Run(Small(), "  ").Locations);
    }

    [Fact]
    public void A_character_filter_selects_all_storages_of_that_character_only()
    {
        var character = Real.Groups[0];

        var all = ItemQueryEngine.Select(Real, new ItemQuery { Character = character.Character, Account = character.Account })!;
        var other = ItemQueryEngine.Select(Real, new ItemQuery { Character = "nobody", Account = character.Account })!;
        var withGroup = ItemQueryEngine.Select(Real, new ItemQuery { GroupId = character.Id, Character = "nobody" })!;

        Assert.Equal(Real.Groups.Where(g => g.Character == character.Character && g.Account == character.Account).Sum(g => g.Items.Count), all.Count);
        Assert.Empty(other);
        Assert.Equal(character.Items.Count, withGroup.Count);
    }}
