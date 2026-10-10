using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using InventoryTracking.Core.Tests.Support;
using InventoryTracking.Server.Hosting;
using InventoryTracking.Server.Tests.Support;

namespace InventoryTracking.Server.Tests;

public class BothModeTests
{
    private static byte[] Export(string storage, string character = "Visitor") =>
        new ExportFileBuilder().Storage(storage).Header("character", character).Header("account", "web").Rows(ExportFileBuilder.Row("Thing", 1)).Build();

    private static IEnumerable<string?> Storages(JsonElement groups) =>
        groups.GetProperty("characters").EnumerateArray()
            .SelectMany(character => character.GetProperty("storages").EnumerateArray())
            .Select(storage => storage.GetProperty("storage").GetString());

    [Fact]
    public async Task Both_has_the_local_setup_and_the_upload_side()
    {
        await using var viewer = await TestViewer.StartAsync(mode: ViewerMode.Both);

        var caps = await viewer.GetJsonAsync("/api/v1/capabilities");
        using var candidates = await viewer.Http.GetAsync("/api/v1/setup/candidates");
        using var preferences = await viewer.Http.GetAsync("/api/v1/preferences");
        using var session = await viewer.Http.PostAsync("/api/v1/sessions", null);

        Assert.Equal("both", caps.GetProperty("mode").GetString());
        Assert.True(caps.GetProperty("import").GetBoolean());
        Assert.True(caps.GetProperty("folderSetup").GetBoolean());
        Assert.Equal(HttpStatusCode.OK, candidates.StatusCode);
        Assert.Equal(HttpStatusCode.OK, preferences.StatusCode);
        Assert.Equal(HttpStatusCode.OK, session.StatusCode);
    }

    [Fact]
    public async Task Without_a_session_both_shows_the_local_folders_only()
    {
        await using var viewer = await TestViewer.StartAsync(mode: ViewerMode.Both);

        var groups = await viewer.GetJsonAsync("/api/v1/groups");
        var status = await viewer.GetJsonAsync("/api/v1/status");

        Assert.Equal(["player", "stash", "scrap", "ammo", "aid"], Storages(groups));
        Assert.Equal(1, status.GetProperty("folders").GetArrayLength());
        Assert.False(status.GetProperty("needsSetup").GetBoolean());
    }

    [Fact]
    public async Task A_session_in_both_mode_adds_its_uploads_to_the_local_data()
    {
        await using var viewer = await TestViewer.StartAsync(mode: ViewerMode.Both);
        await viewer.NewSessionAsync();

        using var upload = await viewer.UploadAsync(("itemdump_web_visitor_stash_1.json", Export("stash")));
        var groups = await viewer.GetJsonAsync("/api/v1/groups");
        var status = await viewer.GetJsonAsync("/api/v1/status");

        Assert.Equal(HttpStatusCode.OK, upload.StatusCode);
        var characters = groups.GetProperty("characters").EnumerateArray().Select(c => c.GetProperty("character").GetString()).Order();
        Assert.Equal(["Tester", "Visitor"], characters);
        Assert.Equal(6, status.GetProperty("fileCount").GetInt32());
        Assert.Equal(1, status.GetProperty("folders").GetArrayLength());
    }

    [Fact]
    public async Task Changing_the_local_folders_is_seen_by_sessions()
    {
        await using var viewer = await TestViewer.StartAsync(mode: ViewerMode.Both);
        await viewer.NewSessionAsync();
        using var upload = await viewer.UploadAsync(("itemdump_web_visitor_stash_1.json", Export("stash")));
        var before = await viewer.GetJsonAsync("/api/v1/status");

        using var removed = await viewer.Http.PostAsJsonAsync("/api/v1/setup/folder/remove", new { path = viewer.DataFolder });
        var after = await viewer.GetJsonAsync("/api/v1/status");

        Assert.Equal(6, before.GetProperty("fileCount").GetInt32());
        Assert.Equal(1, after.GetProperty("fileCount").GetInt32());
        Assert.Equal(0, after.GetProperty("folders").GetArrayLength());
    }
}
