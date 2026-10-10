using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using InventoryTracking.Server.Tests.Support;

namespace InventoryTracking.Server.Tests;

public class ModuleSummaryTests
{
    private static async Task<HttpResponseMessage> Post(TestViewer viewer, object body) =>
        await viewer.Http.PostAsJsonAsync("/api/v1/modules/summary", body, new JsonSerializerOptions(JsonSerializerDefaults.Web));

    [Fact]
    public async Task The_summary_has_tiers_totals_and_the_excluded_list()
    {
        await using var viewer = await TestViewer.StartAsync();

        using var response = await Post(viewer, new { valuable = Array.Empty<object>(), leadersPerModule = 0.3 });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var summary = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.Array, summary.GetProperty("tiers").ValueKind);
        Assert.Equal(summary.GetProperty("tiers").EnumerateArray().Sum(t => t.GetProperty("modules").GetInt64()), summary.GetProperty("modules").GetInt64());
        Assert.Equal(0, summary.GetProperty("valuableCount").GetInt64());
    }

    [Fact]
    public async Task Invalid_settings_and_unknown_groups_are_refused()
    {
        await using var viewer = await TestViewer.StartAsync();

        using var negative = await Post(viewer, new { valuable = Array.Empty<object>(), leadersPerModule = -1 });
        using var badName = await Post(viewer, new { valuable = new[] { new { name = "", stars = (int?)null } }, leadersPerModule = 0.3 });
        using var unknown = await Post(viewer, new { group = "nope", valuable = Array.Empty<object>(), leadersPerModule = 0.3 });

        Assert.Equal(HttpStatusCode.BadRequest, negative.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, badName.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
    }
}
