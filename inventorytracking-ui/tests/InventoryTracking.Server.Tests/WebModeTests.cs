using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using InventoryTracking.Core.Tests.Support;
using InventoryTracking.Server.Hosting;
using InventoryTracking.Server.Tests.Support;

namespace InventoryTracking.Server.Tests;

public class WebModeTests
{
    private static byte[] Real(string name) => File.ReadAllBytes(Path.Combine(Fixtures.RealFolder, name));

    private static (string Name, byte[] Content) AidFile() => ("itemdump_testacc01_tester_aid_1.json", Real("itemdump_testacc01_tester_aid_1.json"));

    private static (string Name, byte[] Content) AmmoFile() => ("itemdump_testacc01_tester_ammo_1.json", Real("itemdump_testacc01_tester_ammo_1.json"));

    private static Task<TestViewer> Web() => TestViewer.StartAsync(withData: false, mode: ViewerMode.Web);

    private static async Task<JsonElement> Json(HttpResponseMessage response) => await response.Content.ReadFromJsonAsync<JsonElement>();

    [Fact]
    public async Task The_capabilities_describe_the_web_version()
    {
        await using var viewer = await Web();

        var caps = await viewer.GetJsonAsync("/api/v1/capabilities");

        Assert.Equal("web", caps.GetProperty("mode").GetString());
        Assert.True(caps.GetProperty("import").GetBoolean());
        Assert.False(caps.GetProperty("folderSetup").GetBoolean());
        Assert.False(caps.GetProperty("folderBrowsing").GetBoolean());
        var limits = caps.GetProperty("importLimits");
        Assert.Equal(256 * 1024, limits.GetProperty("maxFileBytes").GetInt64());
        Assert.Equal(7, limits.GetProperty("sessionDays").GetInt32());
        Assert.Equal(60, limits.GetProperty("memoryMinutes").GetInt32());
    }

    [Fact]
    public async Task Local_folder_setup_and_saved_preferences_do_not_exist_in_the_web_version()
    {
        await using var viewer = await Web();
        await viewer.NewSessionAsync();

        foreach (var path in new[] { "/api/v1/setup/candidates", "/api/v1/setup/dirs?path=C:%5C", "/api/v1/preferences" })
        {
            using var response = await viewer.Http.GetAsync(path);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        using var folder = await viewer.Http.PostAsJsonAsync("/api/v1/setup/folder", new { path = @"C:\", confirm = true });
        Assert.Equal(HttpStatusCode.NotFound, folder.StatusCode);
    }

    [Fact]
    public async Task Data_calls_need_an_existing_session()
    {
        await using var viewer = await Web();

        using var none = await viewer.Http.GetAsync("/api/v1/groups");
        viewer.UseSession("0123456789abcdef0123456789abcdef");
        using var unknown = await viewer.Http.GetAsync("/api/v1/groups");
        viewer.UseSession("../../not-a-session");
        using var malformed = await viewer.Http.GetAsync("/api/v1/status");

        Assert.Equal(HttpStatusCode.Unauthorized, none.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, malformed.StatusCode);
        Assert.Empty(Directory.Exists(viewer.SessionsFolder) ? Directory.GetDirectories(viewer.SessionsFolder) : []);
    }

    [Fact]
    public async Task A_new_session_starts_empty_and_asks_for_files()
    {
        await using var viewer = await Web();
        var id = await viewer.NewSessionAsync();

        var status = await viewer.GetJsonAsync("/api/v1/status");
        var session = await viewer.GetJsonAsync("/api/v1/session");

        Assert.Equal(32, id.Length);
        Assert.True(status.GetProperty("needsSetup").GetBoolean());
        Assert.Equal(0, status.GetProperty("fileCount").GetInt32());
        Assert.Equal(id, session.GetProperty("id").GetString());
        Assert.Equal(0, session.GetProperty("files").GetArrayLength());
        Assert.True(Directory.Exists(Path.Combine(viewer.SessionsFolder, id)));
    }

    [Fact]
    public async Task Several_files_uploaded_at_once_show_up_in_the_lists_at_once()
    {
        await using var viewer = await Web();
        await viewer.NewSessionAsync();

        using var response = await viewer.UploadAsync(AidFile(), AmmoFile());
        var body = await Json(response);
        var status = await viewer.GetJsonAsync("/api/v1/status");
        var groups = await viewer.GetJsonAsync("/api/v1/groups");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.All(body.GetProperty("results").EnumerateArray(), result => Assert.True(result.GetProperty("accepted").GetBoolean()));
        Assert.Equal(2, body.GetProperty("files").GetArrayLength());
        Assert.False(status.GetProperty("needsSetup").GetBoolean());
        Assert.Equal(2, status.GetProperty("fileCount").GetInt32());
        var storages = groups.GetProperty("characters").EnumerateArray()
            .SelectMany(character => character.GetProperty("storages").EnumerateArray())
            .Select(storage => storage.GetProperty("storage").GetString());
        Assert.Equal(["aid", "ammo"], storages.Order());
        var items = await viewer.GetJsonAsync("/api/v1/items?pageSize=5");
        Assert.True(items.GetProperty("totalRows").GetInt32() > 5);
    }

    [Fact]
    public async Task Bad_files_are_refused_one_by_one_while_good_ones_in_the_same_upload_are_kept()
    {
        await using var viewer = await Web();
        await viewer.NewSessionAsync();

        using var response = await viewer.UploadAsync(
            AidFile(),
            ("notes.txt", "hello"u8.ToArray()),
            ("itemdump_x_y_stash_1.json", "{ broken"u8.ToArray()),
            ("itemdump_x_y_stash_2.json", new byte[300 * 1024]));
        var results = (await Json(response)).GetProperty("results").EnumerateArray().ToList();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal([true, false, false, false], results.Select(result => result.GetProperty("accepted").GetBoolean()));
        Assert.Contains("itemdump_", results[1].GetProperty("error").GetString());
        Assert.Contains("JSON", results[2].GetProperty("error").GetString());
        Assert.Contains("larger", results[3].GetProperty("error").GetString());
        Assert.Equal(1, (await viewer.GetJsonAsync("/api/v1/status")).GetProperty("fileCount").GetInt32());
    }

    [Fact]
    public async Task Uploading_a_file_again_replaces_it()
    {
        await using var viewer = await Web();
        await viewer.NewSessionAsync();

        using var first = await viewer.UploadAsync(AidFile());
        using var second = await viewer.UploadAsync(AidFile());

        Assert.Equal(1, (await Json(second)).GetProperty("files").GetArrayLength());
        Assert.Equal(1, (await viewer.GetJsonAsync("/api/v1/status")).GetProperty("fileCount").GetInt32());
    }

    [Fact]
    public async Task Files_can_be_deleted_singly_and_in_groups_and_the_lists_follow()
    {
        await using var viewer = await Web();
        await viewer.NewSessionAsync();
        using var upload = await viewer.UploadAsync(AidFile(), AmmoFile());

        using var one = await viewer.Http.PostAsJsonAsync("/api/v1/session/files/delete", new { names = new[] { AidFile().Name } });
        var afterOne = await Json(one);
        var statusAfterOne = await viewer.GetJsonAsync("/api/v1/status");
        using var rest = await viewer.Http.PostAsJsonAsync("/api/v1/session/files/delete", new { names = new[] { AmmoFile().Name, "itemdump_nothing.json" } });
        var statusAfterAll = await viewer.GetJsonAsync("/api/v1/status");

        Assert.Equal(1, afterOne.GetProperty("deleted").GetInt32());
        Assert.Equal(1, statusAfterOne.GetProperty("fileCount").GetInt32());
        Assert.Equal(1, (await Json(rest)).GetProperty("deleted").GetInt32());
        Assert.Equal(0, statusAfterAll.GetProperty("fileCount").GetInt32());
        Assert.True(statusAfterAll.GetProperty("needsSetup").GetBoolean());
    }

    [Fact]
    public async Task Deleting_without_naming_files_is_refused()
    {
        await using var viewer = await Web();
        await viewer.NewSessionAsync();

        using var response = await viewer.Http.PostAsJsonAsync("/api/v1/session/files/delete", new { names = Array.Empty<string>() });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Sessions_are_isolated_and_a_deleted_session_is_gone()
    {
        await using var viewer = await Web();
        var first = await viewer.NewSessionAsync();
        using var upload = await viewer.UploadAsync(AidFile());
        var second = await viewer.NewSessionAsync();

        var secondStatus = await viewer.GetJsonAsync("/api/v1/status");
        viewer.UseSession(first);
        var firstStatus = await viewer.GetJsonAsync("/api/v1/status");
        using var deleted = await viewer.Http.PostAsync("/api/v1/session/delete", null);
        using var after = await viewer.Http.GetAsync("/api/v1/status");

        Assert.NotEqual(first, second);
        Assert.Equal(0, secondStatus.GetProperty("fileCount").GetInt32());
        Assert.Equal(1, firstStatus.GetProperty("fileCount").GetInt32());
        Assert.Equal(HttpStatusCode.OK, deleted.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, after.StatusCode);
        Assert.False(Directory.Exists(Path.Combine(viewer.SessionsFolder, first)));
        Assert.True(Directory.Exists(Path.Combine(viewer.SessionsFolder, second)));
    }

    [Fact]
    public async Task Whoever_knows_the_link_sees_the_same_data()
    {
        await using var viewer = await Web();
        var id = await viewer.NewSessionAsync();
        using var upload = await viewer.UploadAsync(AidFile());

        using var other = new HttpClient { BaseAddress = viewer.Http.BaseAddress };
        other.DefaultRequestHeaders.Add("X-InventoryTracking", "1");
        other.DefaultRequestHeaders.Add("X-InventoryTracking-Session", id);
        var status = await other.GetFromJsonAsync<JsonElement>("/api/v1/status");

        Assert.Equal(1, status.GetProperty("fileCount").GetInt32());
    }

    [Fact]
    public async Task The_api_still_needs_the_custom_header_and_a_form_upload()
    {
        await using var viewer = await Web();
        var id = await viewer.NewSessionAsync();

        using var bare = new HttpClient { BaseAddress = viewer.Http.BaseAddress };
        bare.DefaultRequestHeaders.Add("X-InventoryTracking-Session", id);
        using var withoutHeader = await bare.GetAsync("/api/v1/status");
        using var notAForm = await viewer.Http.PostAsync("/api/v1/session/files", new StringContent("x"));
        using var empty = await viewer.Http.PostAsync("/api/v1/session/files", new MultipartFormDataContent());

        Assert.Equal(HttpStatusCode.Forbidden, withoutHeader.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, notAForm.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);
    }

    [Fact]
    public async Task A_session_folder_that_cannot_be_written_gives_a_clear_message_instead_of_a_blank_error()
    {
        // The folder's parent is a file, so the folder can never be created (like missing write permission).
        await using var viewer = await TestViewer.StartAsync(
            withData: false,
            mode: ViewerMode.Web,
            sessionsDirectory: scratch =>
            {
                var blocker = Path.Combine(scratch, "blocker");
                File.WriteAllText(blocker, "not a folder");
                return Path.Combine(blocker, "sessions");
            });

        using var response = await viewer.Http.PostAsync("/api/v1/sessions", null);
        var body = await Json(response);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Contains("write access", body.GetProperty("error").GetString());
        Assert.DoesNotContain("blocker", body.GetProperty("error").GetString());
    }

    [Fact]
    public async Task The_page_itself_is_served_without_a_session()
    {
        await using var viewer = await Web();

        using var page = await viewer.Http.GetAsync("/?session=0123456789abcdef0123456789abcdef");

        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        Assert.Contains("text/html", page.Content.Headers.ContentType?.MediaType);
    }
}
