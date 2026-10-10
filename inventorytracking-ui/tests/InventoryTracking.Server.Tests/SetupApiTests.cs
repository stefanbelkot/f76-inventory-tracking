using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using InventoryTracking.Core.Tests.Support;
using InventoryTracking.Server.Tests.Support;

namespace InventoryTracking.Server.Tests;

public class SetupApiTests
{
    private static Task<HttpResponseMessage> PostFolder(TestViewer viewer, string path, bool confirm) =>
        viewer.Http.PostAsJsonAsync("/api/v1/setup/folder", new { path, confirm });

    [Fact]
    public async Task Candidates_come_from_the_machine_description()
    {
        using var drive = new TempFolder();
        var modsdata = drive.Combine("XboxGames", "Fallout 76", "Content", "Data", "modsdata");
        Directory.CreateDirectory(modsdata);
        await using var viewer = await TestViewer.StartAsync(withData: false, machine: new FakeMachine(null, drive.Path));

        var result = await viewer.GetJsonAsync("/api/v1/setup/candidates");

        var candidate = Assert.Single(result.GetProperty("candidates").EnumerateArray());
        Assert.Equal(modsdata, candidate.GetProperty("path").GetString());
        Assert.Equal("Xbox / Game Pass", candidate.GetProperty("label").GetString());
    }

    [Fact]
    public async Task The_folder_browser_lists_sub_folders_and_marks_exports()
    {
        using var root = new TempFolder();
        Directory.CreateDirectory(root.Combine("plain"));
        Directory.CreateDirectory(root.Combine("game"));
        File.WriteAllText(root.Combine("game", "itemdump_a_b_stash_1.json"), "{}");
        await using var viewer = await TestViewer.StartAsync(withData: false);

        var listing = await viewer.GetJsonAsync($"/api/v1/setup/dirs?path={Uri.EscapeDataString(root.Path)}");

        var dirs = listing.GetProperty("dirs").EnumerateArray().ToList();
        Assert.Equal(["game", "plain"], dirs.Select(d => d.GetProperty("name").GetString()));
        Assert.Equal([true, false], dirs.Select(d => d.GetProperty("hasExports").GetBoolean()));
    }

    [Theory]
    [InlineData("relative")]
    [InlineData("\\\\server\\share")]
    [InlineData("C:\\no\\such\\folder")]
    public async Task The_folder_browser_rejects_paths_it_cannot_list(string path)
    {
        await using var viewer = await TestViewer.StartAsync(withData: false);

        using var response = await viewer.Http.GetAsync($"/api/v1/setup/dirs?path={Uri.EscapeDataString(path)}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Choosing_a_game_install_folder_resolves_it_first_and_saves_only_after_confirmation()
    {
        using var install = new TempFolder();
        var modsdata = install.Combine("Data", "modsdata");
        Directory.CreateDirectory(modsdata);
        File.Copy(Directory.GetFiles(Fixtures.RealFolder).First(), Path.Combine(modsdata, "itemdump_testacc01_tester_aid_1.json"));
        await using var viewer = await TestViewer.StartAsync(withData: false);

        using var preview = await PostFolder(viewer, install.Path, confirm: false);
        var previewBody = await preview.Content.ReadFromJsonAsync<JsonElement>();
        var statusBefore = await viewer.GetJsonAsync("/api/v1/status");

        Assert.Equal(modsdata, previewBody.GetProperty("path").GetString());
        Assert.Equal("installFolder", previewBody.GetProperty("kind").GetString());
        Assert.False(previewBody.GetProperty("saved").GetBoolean());
        Assert.True(statusBefore.GetProperty("needsSetup").GetBoolean());
        Assert.False(File.Exists(Path.Combine(viewer.SettingsFolder, "settings.json")));

        using var confirmed = await PostFolder(viewer, install.Path, confirm: true);
        var statusAfter = await viewer.GetJsonAsync("/api/v1/status");

        Assert.True((await confirmed.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("saved").GetBoolean());
        Assert.False(statusAfter.GetProperty("needsSetup").GetBoolean());
        Assert.Equal([modsdata], statusAfter.GetProperty("folders").EnumerateArray().Select(f => f.GetProperty("path").GetString()));
        Assert.Contains(modsdata.Replace("\\", "\\\\"), File.ReadAllText(Path.Combine(viewer.SettingsFolder, "settings.json")));
        Assert.Single((await viewer.GetJsonAsync("/api/v1/groups")).GetProperty("characters").EnumerateArray());
    }

    private static string MakeExportFolder(TempFolder root, string name, string fixtureFile)
    {
        var folder = root.Combine(name, "Data", "modsdata");
        Directory.CreateDirectory(folder);
        File.Copy(Path.Combine(Fixtures.RealFolder, fixtureFile), Path.Combine(folder, fixtureFile));
        return folder;
    }

    private static IEnumerable<string?> FolderPaths(JsonElement status) =>
        status.GetProperty("folders").EnumerateArray().Select(f => f.GetProperty("path").GetString());

    [Fact]
    public async Task Folders_are_added_to_the_list_and_read_together()
    {
        using var roots = new TempFolder();
        var steam = MakeExportFolder(roots, "steam", "itemdump_testacc01_tester_aid_1.json");
        var xbox = MakeExportFolder(roots, "xbox", "itemdump_testacc01_tester_ammo_1.json");
        await using var viewer = await TestViewer.StartAsync(withData: false);

        using var first = await PostFolder(viewer, steam, confirm: true);
        using var second = await PostFolder(viewer, Path.GetDirectoryName(Path.GetDirectoryName(xbox))!, confirm: true);
        var status = await viewer.GetJsonAsync("/api/v1/status");
        var storages = (await viewer.GetJsonAsync("/api/v1/groups")).GetProperty("characters").EnumerateArray()
            .SelectMany(c => c.GetProperty("storages").EnumerateArray())
            .Select(s => s.GetProperty("storage").GetString())
            .Order();

        Assert.Equal([steam, xbox], FolderPaths(status));
        Assert.Equal(2, status.GetProperty("fileCount").GetInt32());
        Assert.Equal(["aid", "ammo"], storages);
    }

    [Fact]
    public async Task Adding_the_same_folder_again_keeps_one_entry_whatever_the_case()
    {
        using var roots = new TempFolder();
        var folder = MakeExportFolder(roots, "steam", "itemdump_testacc01_tester_aid_1.json");
        await using var viewer = await TestViewer.StartAsync(withData: false);

        using var first = await PostFolder(viewer, folder, confirm: true);
        using var again = await PostFolder(viewer, folder.ToUpperInvariant() + "\\", confirm: true);

        Assert.Single(FolderPaths(await viewer.GetJsonAsync("/api/v1/status")));
    }

    [Fact]
    public async Task A_folder_can_be_removed_and_without_any_folder_setup_is_needed_again()
    {
        using var roots = new TempFolder();
        var steam = MakeExportFolder(roots, "steam", "itemdump_testacc01_tester_aid_1.json");
        var xbox = MakeExportFolder(roots, "xbox", "itemdump_testacc01_tester_ammo_1.json");
        await using var viewer = await TestViewer.StartAsync(withData: false);
        using var a = await PostFolder(viewer, steam, confirm: true);
        using var b = await PostFolder(viewer, xbox, confirm: true);

        using var removed = await viewer.Http.PostAsJsonAsync("/api/v1/setup/folder/remove", new { path = steam });
        var afterOne = await viewer.GetJsonAsync("/api/v1/status");
        using var removedLast = await viewer.Http.PostAsJsonAsync("/api/v1/setup/folder/remove", new { path = xbox });
        var afterAll = await viewer.GetJsonAsync("/api/v1/status");

        Assert.Equal([xbox], FolderPaths(afterOne));
        Assert.Equal(1, afterOne.GetProperty("fileCount").GetInt32());
        Assert.True(afterAll.GetProperty("needsSetup").GetBoolean());
        Assert.Empty(FolderPaths(afterAll));
    }

    [Fact]
    public async Task A_folder_that_vanished_is_reported_and_does_not_stop_the_others()
    {
        using var roots = new TempFolder();
        var steam = MakeExportFolder(roots, "steam", "itemdump_testacc01_tester_aid_1.json");
        var gone = MakeExportFolder(roots, "gone", "itemdump_testacc01_tester_ammo_1.json");
        await using var viewer = await TestViewer.StartAsync(withData: false);
        using var a = await PostFolder(viewer, steam, confirm: true);
        using var b = await PostFolder(viewer, gone, confirm: true);
        Directory.Delete(Path.GetDirectoryName(Path.GetDirectoryName(gone))!, recursive: true);

        using var refresh = await viewer.Http.PostAsync("/api/v1/refresh", null);
        var status = await viewer.GetJsonAsync("/api/v1/status");

        var entry = status.GetProperty("folders").EnumerateArray().Single(f => f.GetProperty("path").GetString() == gone);
        Assert.False(entry.GetProperty("exists").GetBoolean());
        Assert.Contains(status.GetProperty("issues").EnumerateArray(), issue => issue.GetProperty("file").GetString() == gone);
        Assert.Single((await viewer.GetJsonAsync("/api/v1/groups")).GetProperty("characters").EnumerateArray());
    }

    [Fact]
    public async Task A_folder_without_a_modsdata_folder_is_refused()
    {
        using var empty = new TempFolder();
        await using var viewer = await TestViewer.StartAsync(withData: false);

        using var response = await PostFolder(viewer, empty.Path, confirm: true);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True((await viewer.GetJsonAsync("/api/v1/status")).GetProperty("needsSetup").GetBoolean());
    }

    [Fact]
    public async Task Preferences_round_trip_through_the_settings_file()
    {
        await using var viewer = await TestViewer.StartAsync();

        using var saved = await viewer.Http.PutAsJsonAsync("/api/v1/preferences", new { theme = "dark", columns = new[] { "name", "count" } });
        var loaded = await viewer.GetJsonAsync("/api/v1/preferences");

        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        Assert.Equal("dark", loaded.GetProperty("theme").GetString());
        Assert.Equal(["name", "count"], loaded.GetProperty("columns").EnumerateArray().Select(c => c.GetString()));
        Assert.Equal(["settings.json"], Directory.GetFiles(viewer.SettingsFolder).Select(Path.GetFileName));
    }

    [Theory]
    [InlineData("""{"theme":"neon","columns":null}""")]
    [InlineData("""{"theme":"dark","columns":[""]}""")]
    public async Task Invalid_preferences_are_refused(string json)
    {
        await using var viewer = await TestViewer.StartAsync();
        using var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

        using var response = await viewer.Http.PutAsync("/api/v1/preferences", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Using_the_api_never_changes_the_data_folder_and_writes_nothing_but_settings()
    {
        await using var viewer = await TestViewer.StartAsync();
        var before = Snapshot(viewer.DataFolder!);

        await viewer.GetJsonAsync("/api/v1/groups");
        await viewer.GetJsonAsync("/api/v1/items?pageSize=1000");
        using var export = await viewer.Http.GetAsync("/api/v1/export?format=csv");
        using var refresh = await viewer.Http.PostAsync("/api/v1/refresh", null);

        Assert.Equal(before, Snapshot(viewer.DataFolder!));
        Assert.False(Directory.Exists(viewer.SettingsFolder) && Directory.GetFiles(viewer.SettingsFolder).Length > 0);
    }

    private static List<string> Snapshot(string folder) =>
        Directory.GetFileSystemEntries(folder)
            .Order()
            .Select(path => $"{Path.GetFileName(path)}|{new FileInfo(path).Length}|{File.GetLastWriteTimeUtc(path).Ticks}")
            .ToList();
}
