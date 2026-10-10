using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using InventoryTracking.Server.Tests.Support;

namespace InventoryTracking.Server.Tests;

public class ApiTests
{
    private static async Task<string> GroupId(TestViewer viewer, string storage)
    {
        var groups = await viewer.GetJsonAsync("/api/v1/groups");
        return groups.GetProperty("characters")[0].GetProperty("storages").EnumerateArray()
            .Single(g => g.GetProperty("storage").GetString() == storage).GetProperty("id").GetString()!;
    }

    [Fact]
    public async Task Capabilities_describe_the_local_mode_and_the_item_types()
    {
        await using var viewer = await TestViewer.StartAsync();

        var capabilities = await viewer.GetJsonAsync("/api/v1/capabilities");

        Assert.Equal("local", capabilities.GetProperty("mode").GetString());
        Assert.True(capabilities.GetProperty("folderBrowsing").GetBoolean());
        Assert.True(capabilities.GetProperty("shutdown").GetBoolean());
        var types = capabilities.GetProperty("itemTypes").EnumerateArray().Select(t => t.GetProperty("id").GetString()).ToList();
        Assert.Contains("weapon", types);
        Assert.Equal("other", types[^1]);
    }

    [Fact]
    public async Task Status_reports_the_folder_the_data_version_and_no_issues()
    {
        await using var viewer = await TestViewer.StartAsync();

        var status = await viewer.GetJsonAsync("/api/v1/status");

        Assert.False(status.GetProperty("needsSetup").GetBoolean());
        Assert.Equal("commandLine", status.GetProperty("folderOrigin").GetString());
        Assert.Equal(5, status.GetProperty("fileCount").GetInt32());
        Assert.Equal(0, status.GetProperty("issues").GetArrayLength());
        Assert.True(status.GetProperty("dataVersion").GetInt64() > 0);
    }

    [Fact]
    public async Task Without_a_folder_the_status_asks_for_setup()
    {
        await using var viewer = await TestViewer.StartAsync(withData: false);

        var status = await viewer.GetJsonAsync("/api/v1/status");

        Assert.True(status.GetProperty("needsSetup").GetBoolean());
        Assert.Empty(status.GetProperty("folders").EnumerateArray());
    }

    [Fact]
    public async Task Groups_list_the_five_storages_of_the_character_as_complete()
    {
        await using var viewer = await TestViewer.StartAsync();

        var groups = await viewer.GetJsonAsync("/api/v1/groups");

        var character = Assert.Single(groups.GetProperty("characters").EnumerateArray());
        var storages = character.GetProperty("storages").EnumerateArray().ToList();
        Assert.Equal(["player", "stash", "scrap", "ammo", "aid"], storages.Select(s => s.GetProperty("storage").GetString()));
        Assert.All(storages, s => Assert.Equal("complete", s.GetProperty("status").GetString()));
        Assert.Equal(
            storages.Select(s => s.GetProperty("rows").GetInt32()),
            storages.Select(s => s.GetProperty("totals").GetProperty("rows").GetInt32()));
        Assert.All(storages, s => Assert.False(s.TryGetProperty("items", out _)));
        Assert.All(storages, s => Assert.Equal(JsonValueKind.Array, s.GetProperty("currencies").ValueKind));
    }

    [Fact]
    public async Task Items_carry_the_vendor_fields_and_the_vendor_filter_is_accepted()
    {
        await using var viewer = await TestViewer.StartAsync();

        var all = await viewer.GetJsonAsync("/api/v1/items?pageSize=5");
        var vendors = await viewer.GetJsonAsync("/api/v1/items?vendor=true");

        var first = all.GetProperty("items")[0];
        Assert.False(first.GetProperty("vendor").GetBoolean());
        Assert.Equal(JsonValueKind.Null, first.GetProperty("price").ValueKind);
        Assert.Equal(0, vendors.GetProperty("totalRows").GetInt32());
        using var bad = await viewer.Http.GetAsync("/api/v1/items?vendor=maybe");
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
    }

    [Fact]
    public async Task Legendary_modules_have_a_clean_name_and_can_be_found_by_stars()
    {
        await using var viewer = await TestViewer.StartAsync();

        var page = await viewer.GetJsonAsync("/api/v1/items?type=mods&stars=1,2,3,4&pageSize=1000");

        var items = page.GetProperty("items").EnumerateArray().ToList();
        Assert.NotEmpty(items);
        Assert.All(items, item => Assert.DoesNotContain('¬', item.GetProperty("name").GetString()!));
        Assert.All(items, item => Assert.InRange(item.GetProperty("stars").GetInt32(), 1, 4));
    }

    [Fact]
    public async Task Items_are_filtered_sorted_and_paged_by_the_server()
    {
        await using var viewer = await TestViewer.StartAsync();
        var stash = await GroupId(viewer, "stash");

        var page = await viewer.GetJsonAsync($"/api/v1/items?group={stash}&legendary=true&sort=-stars&pageSize=10");

        Assert.Equal(10, page.GetProperty("items").GetArrayLength());
        Assert.True(page.GetProperty("totalRows").GetInt32() >= 10);
        var stars = page.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("stars").GetInt32()).ToList();
        Assert.Equal(stars.OrderDescending(), stars);
        Assert.All(page.GetProperty("items").EnumerateArray(), i => Assert.True(i.GetProperty("legendary").GetBoolean()));
        Assert.False(page.GetProperty("items")[0].TryGetProperty("desc", out _));
    }

    [Fact]
    public async Task Text_and_type_filters_combine_and_totals_follow_the_filter()
    {
        await using var viewer = await TestViewer.StartAsync();

        var all = await viewer.GetJsonAsync("/api/v1/items?pageSize=1");
        var weapons = await viewer.GetJsonAsync("/api/v1/items?type=weapon&pageSize=1");
        var search = await viewer.GetJsonAsync("/api/v1/items?type=weapon&q=zzzzzz-no-match");

        Assert.True(weapons.GetProperty("totalRows").GetInt32() < all.GetProperty("totalRows").GetInt32());
        Assert.Equal(weapons.GetProperty("totalRows").GetInt32(), weapons.GetProperty("totals").GetProperty("rows").GetInt32());
        Assert.Equal(0, search.GetProperty("totalRows").GetInt32());
    }

    [Theory]
    [InlineData("/api/v1/items?sort=bogus")]
    [InlineData("/api/v1/items?legendary=maybe")]
    [InlineData("/api/v1/items?page=abc")]
    [InlineData("/api/v1/items?stars=x")]
    [InlineData("/api/v1/export?format=xml")]
    [InlineData("/api/v1/links")]
    public async Task Malformed_parameters_give_a_400(string path)
    {
        await using var viewer = await TestViewer.StartAsync();

        using var response = await viewer.Http.GetAsync(path);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/v1/items?group=nope")]
    [InlineData("/api/v1/items/nope.1")]
    [InlineData("/api/v1/links?item=nope.1")]
    [InlineData("/api/v1/export?format=csv&group=nope")]
    [InlineData("/api/v1/unknown")]
    public async Task Unknown_things_give_a_404(string path)
    {
        await using var viewer = await TestViewer.StartAsync();

        using var response = await viewer.Http.GetAsync(path);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task A_legendary_item_detail_lists_its_effects_by_star_and_links()
    {
        await using var viewer = await TestViewer.StartAsync();
        var list = await viewer.GetJsonAsync("/api/v1/items?legendary=true&q=reload&pageSize=1");
        var key = list.GetProperty("items")[0].GetProperty("key").GetString();

        var item = await viewer.GetJsonAsync($"/api/v1/items/{key}");
        var links = await viewer.GetJsonAsync($"/api/v1/links?item={key}");

        var effects = item.GetProperty("effects").EnumerateArray().ToList();
        Assert.NotEmpty(effects);
        Assert.Contains(effects, e => e.GetProperty("star").ValueKind == JsonValueKind.Number);
        Assert.StartsWith("https://www.google.com/search?q=Fallout%2076%20", links.GetProperty("name").GetProperty("googleUrl").GetString());
        Assert.NotEqual(0, links.GetProperty("effects").GetArrayLength());
    }

    [Fact]
    public async Task Links_with_edited_terms_use_those_terms()
    {
        await using var viewer = await TestViewer.StartAsync();

        var links = await viewer.GetJsonAsync("/api/v1/links?q=Red%20Terror");

        Assert.Equal("Red Terror", links.GetProperty("name").GetProperty("terms").GetString());
        Assert.Equal(
            "https://fallout.fandom.com/wiki/Special:Search?query=Red%20Terror",
            links.GetProperty("name").GetProperty("wikiUrl").GetString());
    }

    [Fact]
    public async Task Search_counts_matches_per_storage()
    {
        await using var viewer = await TestViewer.StartAsync();

        var result = await viewer.GetJsonAsync("/api/v1/search?q=a");

        Assert.True(result.GetProperty("rows").GetInt32() > 0);
        Assert.NotEmpty(result.GetProperty("locations").EnumerateArray());
    }

    [Fact]
    public async Task Export_returns_the_filtered_view_as_csv_and_json()
    {
        await using var viewer = await TestViewer.StartAsync();
        var scrap = await GroupId(viewer, "scrap");
        var rows = (await viewer.GetJsonAsync($"/api/v1/items?group={scrap}")).GetProperty("totalRows").GetInt32();

        using var csv = await viewer.Http.GetAsync($"/api/v1/export?format=csv&group={scrap}");
        using var json = await viewer.Http.GetAsync($"/api/v1/export?format=json&group={scrap}");

        Assert.Equal("text/csv", csv.Content.Headers.ContentType?.MediaType);
        Assert.Equal(rows + 1, Encoding.UTF8.GetString(await csv.Content.ReadAsByteArrayAsync()).TrimStart('﻿').Split("\r\n", StringSplitOptions.RemoveEmptyEntries).Length);
        var items = await json.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(rows, items.GetArrayLength());
    }

    [Fact]
    public async Task Refresh_reloads_and_changes_the_data_version()
    {
        await using var viewer = await TestViewer.StartAsync();
        var before = (await viewer.GetJsonAsync("/api/v1/status")).GetProperty("dataVersion").GetInt64();

        using var response = await viewer.Http.PostAsync("/api/v1/refresh", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True((await viewer.GetJsonAsync("/api/v1/status")).GetProperty("dataVersion").GetInt64() > before);
    }

    [Fact]
    public async Task A_new_export_in_the_folder_is_picked_up_without_a_request()
    {
        await using var viewer = await TestViewer.StartAsync();
        var source = Directory.GetFiles(viewer.DataFolder!, "*_aid_1.json").Single();

        File.Copy(source, Path.Combine(viewer.DataFolder!, "itemdump_testacc01_other_aid_1.json"));

        var seen = SpinWait.SpinUntil(
            () => viewer.GetJsonAsync("/api/v1/status").Result.GetProperty("fileCount").GetInt32() == 6,
            TimeSpan.FromSeconds(10));
        Assert.True(seen);
        var status = await viewer.GetJsonAsync("/api/v1/status");
        Assert.Equal("duplicatePart", Assert.Single(status.GetProperty("issues").EnumerateArray()).GetProperty("kind").GetString());
    }
}
