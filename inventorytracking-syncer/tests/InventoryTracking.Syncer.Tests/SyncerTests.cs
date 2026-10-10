using System.Net;
using System.Text;
using InventoryTracking.Syncer.Settings;
using InventoryTracking.Syncer.Startup;
using InventoryTracking.Syncer.Sync;

namespace InventoryTracking.Syncer.Tests;

public class SessionLinkTests
{
    private const string Id = "fa9ed8dc0eed46a1af4bc5c148c5a3a3";

    [Theory]
    [InlineData("https://f76inventorytracker.bestesoftware.eu/?session=fa9ed8dc0eed46a1af4bc5c148c5a3a3")]
    [InlineData("  https://f76inventorytracker.bestesoftware.eu/?x=1&session=FA9ED8DC0EED46A1AF4BC5C148C5A3A3  ")]
    [InlineData("fa9ed8dc0eed46a1af4bc5c148c5a3a3")]
    [InlineData("FA9ED8DC0EED46A1AF4BC5C148C5A3A3")]
    public void Links_and_bare_ids_give_the_id(string text)
    {
        Assert.True(SessionLink.TryParse(text, out var id, out _));
        Assert.Equal(Id, id);
    }

    [Theory]
    [InlineData("")]
    [InlineData("hello")]
    [InlineData("https://example.org/?session=short")]
    [InlineData("ftp://example.org/?session=fa9ed8dc0eed46a1af4bc5c148c5a3a3")]
    public void Garbage_is_rejected(string text) => Assert.False(SessionLink.TryParse(text, out _, out _));

    [Fact]
    public void A_link_gives_its_server_address()
    {
        SessionLink.TryParse($"https://host.example/app/?session={Id}", out _, out var server);

        Assert.Equal("https://host.example/app/", server);
    }

    [Theory]
    [InlineData("https://host.example", "https://host.example/")]
    [InlineData("http://127.0.0.1:8080/", "http://127.0.0.1:8080/")]
    [InlineData("http://host.example/", null)]
    [InlineData("nonsense", null)]
    public void Server_addresses_must_be_https_unless_local(string text, string? expected) => Assert.Equal(expected, SessionLink.NormalizeServer(text));
}

public class SyncPlannerTests
{
    private static LocalFile File(string name, string content = "{}", int hashSeed = 0) =>
        new(name, "C:\\x\\" + name, DateTime.UtcNow, Encoding.UTF8.GetBytes(content), LocalFile.HashOf(Encoding.UTF8.GetBytes(content + hashSeed)));

    private static ScanResult Scan(bool available, params LocalFile[] files) =>
        new(files, files.Select(f => f.Name).ToHashSet(StringComparer.OrdinalIgnoreCase), [], [], available);

    [Fact]
    public void New_and_changed_files_are_uploaded_unchanged_ones_are_not()
    {
        var same = File("itemdump_a_b_stash_1.json");
        var changed = File("itemdump_a_b_player_1.json");
        var fresh = File("itemdump_a_b_aid_1.json");
        var known = new Dictionary<string, string> { [same.Name] = same.Hash, [changed.Name] = "old" };

        var plan = SyncPlanner.Plan(Scan(true, same, changed, fresh), known, new HashSet<string>(), new SyncLimits());

        Assert.Equal([fresh.Name, changed.Name], plan.Uploads.Select(f => f.Name));
        Assert.Empty(plan.Deletes);
    }

    [Fact]
    public void A_vanished_file_is_deleted_only_when_all_folders_are_available()
    {
        var known = new Dictionary<string, string> { ["itemdump_a_b_gone_1.json"] = "h" };

        var available = SyncPlanner.Plan(Scan(true), known, new HashSet<string>(), new SyncLimits());
        var missingFolder = SyncPlanner.Plan(Scan(false), known, new HashSet<string>(), new SyncLimits());

        Assert.Equal(["itemdump_a_b_gone_1.json"], available.Deletes);
        Assert.Empty(missingFolder.Deletes);
    }

    [Fact]
    public void A_locked_file_that_still_exists_is_not_deleted()
    {
        var scan = new ScanResult([], new HashSet<string> { "itemdump_a_b_busy_1.json" }, ["itemdump_a_b_busy_1.json"], [], true);

        var plan = SyncPlanner.Plan(scan, new Dictionary<string, string> { ["itemdump_a_b_busy_1.json"] = "h" }, new HashSet<string>(), new SyncLimits());

        Assert.Empty(plan.Deletes);
    }

    [Fact]
    public void Bad_names_empty_files_oversized_files_and_refused_content_are_skipped()
    {
        var bad = File("notes.json");
        var empty = new LocalFile("itemdump_a_b_x_1.json", "p", DateTime.UtcNow, [], "h");
        var big = new LocalFile("itemdump_a_b_y_1.json", "p", DateTime.UtcNow, new byte[300_000], "h2");
        var refused = File("itemdump_a_b_z_1.json");

        var plan = SyncPlanner.Plan(Scan(true, bad, empty, big, refused), new Dictionary<string, string>(),
            new HashSet<string> { $"{refused.Name}|{refused.Hash}" }, new SyncLimits());

        Assert.Empty(plan.Uploads);
        Assert.Equal(3, plan.Skipped.Count);
    }

    [Fact]
    public void The_session_file_limit_is_respected()
    {
        var files = Enumerable.Range(0, 5).Select(i => File($"itemdump_a_b_s{i}_1.json")).ToArray();

        var plan = SyncPlanner.Plan(Scan(true, files), new Dictionary<string, string>(), new HashSet<string>(), new SyncLimits(MaxFiles: 3));

        Assert.Equal(3, plan.Uploads.Count);
        Assert.Equal(2, plan.Skipped.Count);
    }
}

public class FolderScannerTests
{
    [Fact]
    public void The_newest_copy_of_a_name_wins_and_other_files_are_ignored()
    {
        using var dir = new TempDir();
        var steam = dir.Sub("steam");
        var xbox = dir.Sub("xbox");
        var name = "itemdump_a_b_stash_1.json";
        System.IO.File.WriteAllText(Path.Combine(steam, name), "{\"v\":\"steam\"}");
        System.IO.File.WriteAllText(Path.Combine(xbox, name), "{\"v\":\"xbox\"}");
        System.IO.File.SetLastWriteTimeUtc(Path.Combine(steam, name), DateTime.UtcNow.AddHours(-2));
        System.IO.File.WriteAllText(Path.Combine(xbox, "other.txt"), "x");

        var scan = new FolderScanner().Scan([steam, xbox]);

        var file = Assert.Single(scan.Files);
        Assert.Contains("xbox", Encoding.UTF8.GetString(file.Content));
        Assert.True(scan.AllFoldersAvailable);
    }

    [Fact]
    public void A_missing_folder_marks_the_scan_as_incomplete()
    {
        using var dir = new TempDir();

        var scan = new FolderScanner().Scan([Path.Combine(dir.Path, "nowhere")]);

        Assert.False(scan.AllFoldersAvailable);
    }

    [Fact]
    public void A_file_locked_for_reading_is_reported_but_still_present()
    {
        using var dir = new TempDir();
        var folder = dir.Sub("f");
        var path = Path.Combine(folder, "itemdump_a_b_stash_1.json");
        using var hold = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
        hold.WriteByte(1);

        var scan = new FolderScanner().Scan([folder]);

        Assert.Empty(scan.Files);
        Assert.Contains("itemdump_a_b_stash_1.json", scan.Locked);
        Assert.Contains("itemdump_a_b_stash_1.json", scan.Present);
    }
}

public class SettingsStoreTests
{
    [Fact]
    public void Settings_round_trip_and_keep_unknown_fields()
    {
        using var dir = new TempDir();
        Directory.CreateDirectory(dir.Path);
        System.IO.File.WriteAllText(Path.Combine(dir.Path, "settings.json"), "{\"session\":\"" + new string('a', 32) + "\",\"future\":{\"x\":1}}");

        var store = new SettingsStore(dir.Path);
        store.Update(s => s.StartWithWindows = true);
        var again = new SettingsStore(dir.Path);

        Assert.True(again.Current.StartWithWindows);
        Assert.Equal(new string('a', 32), again.Current.Session);
        Assert.Contains("future", System.IO.File.ReadAllText(Path.Combine(dir.Path, "settings.json")));
    }

    [Fact]
    public void A_damaged_file_is_kept_as_bad_and_empty_settings_are_used()
    {
        using var dir = new TempDir();
        Directory.CreateDirectory(dir.Path);
        System.IO.File.WriteAllText(Path.Combine(dir.Path, "settings.json"), "{ not json");

        var store = new SettingsStore(dir.Path);

        Assert.False(store.Existed);
        Assert.Equal("", store.Current.Session);
        Assert.True(System.IO.File.Exists(Path.Combine(dir.Path, "settings.json.bad")));
    }
}

public class SyncEngineTests
{
    private const string Session = "fa9ed8dc0eed46a1af4bc5c148c5a3a3";

    private static (SyncEngine Engine, FakeClient Client, SettingsStore Store, string Folder) Create(TempDir dir)
    {
        var folder = dir.Sub("modsdata");
        var store = new SettingsStore(Path.Combine(dir.Path, "settings"));
        store.Update(s =>
        {
            s.Session = Session;
            s.Folders = [new WatchedFolder { Path = folder }];
        });
        var client = new FakeClient();
        return (new SyncEngine(store, _ => client, new FolderScanner(), new ListLog()), client, store, folder);
    }

    [Fact]
    public async Task Uploads_once_then_only_changes_and_mirrors_deletions()
    {
        using var dir = new TempDir();
        var (engine, client, store, folder) = Create(dir);
        var a = Path.Combine(folder, "itemdump_a_b_stash_1.json");
        var b = Path.Combine(folder, "itemdump_a_b_player_1.json");
        System.IO.File.WriteAllText(a, "{\"n\":1}");
        System.IO.File.WriteAllText(b, "{\"n\":2}");

        var first = await engine.RunOnceAsync(CancellationToken.None);
        var second = await engine.RunOnceAsync(CancellationToken.None);
        System.IO.File.WriteAllText(a, "{\"n\":3}");
        await engine.RunOnceAsync(CancellationToken.None);
        System.IO.File.Delete(b);
        var last = await engine.RunOnceAsync(CancellationToken.None);

        Assert.Equal(SyncState.Ok, first.State);
        Assert.Equal(2, first.Uploaded);
        Assert.Equal(0, second.Uploaded);
        Assert.Equal(3, client.Uploaded.Count);
        Assert.Equal(["itemdump_a_b_player_1.json"], client.Deleted);
        Assert.Equal(1, last.Deleted);
        Assert.Single(store.Current.Uploaded[Session]);
    }

    [Fact]
    public async Task Files_the_tool_did_not_upload_are_never_deleted_remotely()
    {
        using var dir = new TempDir();
        var (engine, client, _, _) = Create(dir);

        var outcome = await engine.RunOnceAsync(CancellationToken.None);

        Assert.Equal(SyncState.Ok, outcome.State);
        Assert.Empty(client.Deleted);
    }

    [Fact]
    public async Task A_missing_folder_does_not_wipe_the_session()
    {
        using var dir = new TempDir();
        var (engine, client, store, folder) = Create(dir);
        System.IO.File.WriteAllText(Path.Combine(folder, "itemdump_a_b_stash_1.json"), "{}");
        await engine.RunOnceAsync(CancellationToken.None);
        Directory.Delete(folder, recursive: true);

        await engine.RunOnceAsync(CancellationToken.None);

        Assert.Empty(client.Deleted);
        Assert.Single(store.Current.Uploaded[Session]);
    }

    [Fact]
    public async Task An_unknown_session_gives_session_expired_and_network_trouble_gives_offline_with_retry()
    {
        using var dir = new TempDir();
        var (engine, client, _, folder) = Create(dir);
        System.IO.File.WriteAllText(Path.Combine(folder, "itemdump_a_b_stash_1.json"), "{}");

        client.Fail = new SessionException(SessionProblem.Unknown, "gone");
        var expired = await engine.RunOnceAsync(CancellationToken.None);
        client.Fail = new SessionException(SessionProblem.Network, "down");
        var offline = await engine.RunOnceAsync(CancellationToken.None);

        Assert.Equal(SyncState.SessionExpired, expired.State);
        Assert.Equal(SyncState.Offline, offline.State);
        Assert.True(offline.Retry);
    }

    [Fact]
    public async Task A_refused_file_is_not_sent_again_until_it_changes()
    {
        using var dir = new TempDir();
        var (engine, client, _, folder) = Create(dir);
        var path = Path.Combine(folder, "itemdump_a_b_stash_1.json");
        System.IO.File.WriteAllText(path, "{\"n\":1}");
        client.Refuse.Add("itemdump_a_b_stash_1.json");

        var first = await engine.RunOnceAsync(CancellationToken.None);
        await engine.RunOnceAsync(CancellationToken.None);
        System.IO.File.WriteAllText(path, "{\"n\":2}");
        await engine.RunOnceAsync(CancellationToken.None);

        Assert.Equal(SyncState.Problem, first.State);
        Assert.Equal(2, client.UploadCalls);
    }

    [Fact]
    public async Task Without_a_session_or_folder_nothing_runs()
    {
        using var dir = new TempDir();
        var store = new SettingsStore(Path.Combine(dir.Path, "s"));
        var engine = new SyncEngine(store, _ => new FakeClient(), new FolderScanner(), new ListLog());

        Assert.Equal(SyncState.NotConfigured, (await engine.RunOnceAsync(CancellationToken.None)).State);
    }
}

public class AutostartTests
{
    private sealed class FakeKey : IRunKey
    {
        public string? Value { get; set; }

        public string? Get() => Value;

        public void Set(string command) => Value = command;

        public void Remove() => Value = null;
    }

    [Fact]
    public void Enable_writes_the_background_command_and_disable_removes_it()
    {
        var key = new FakeKey();
        var autostart = new Autostart(key, @"C:\Tools\InventoryTrackingSyncer.exe");

        autostart.Set(true);
        var enabled = autostart.IsEnabled;
        autostart.Set(false);

        Assert.True(enabled);
        Assert.Null(key.Value);
        Assert.Equal("\"C:\\Tools\\InventoryTrackingSyncer.exe\" --background", autostart.Command);
    }

    [Fact]
    public void Reconcile_repairs_a_moved_exe_but_only_when_wanted()
    {
        var key = new FakeKey { Value = "\"C:\\Old\\x.exe\" --background" };
        var autostart = new Autostart(key, @"C:\New\x.exe");

        autostart.Reconcile(wanted: false);
        Assert.Equal("\"C:\\Old\\x.exe\" --background", key.Value);
        autostart.Reconcile(wanted: true);

        Assert.Equal(autostart.Command, key.Value);
    }
}

public class SessionClientTests
{
    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(respond(request));
        }
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string json) =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    [Fact]
    public async Task Upload_sends_the_headers_and_files_and_reads_the_results()
    {
        var handler = new Handler(_ => Json(HttpStatusCode.OK, "{\"results\":[{\"name\":\"itemdump_a_b_stash_1.json\",\"accepted\":true,\"error\":null}],\"files\":[]}"));
        var client = new SessionClient("https://host.example/", handler);
        var file = new LocalFile("itemdump_a_b_stash_1.json", "p", DateTime.UtcNow, Encoding.UTF8.GetBytes("{}"), "h");

        var results = await client.UploadAsync("fa9ed8dc0eed46a1af4bc5c148c5a3a3", [file], CancellationToken.None);

        var request = Assert.Single(handler.Requests);
        Assert.Equal("https://host.example/api/v1/session/files", request.RequestUri!.ToString());
        Assert.Equal("1", request.Headers.GetValues("X-InventoryTracking").Single());
        Assert.Equal("fa9ed8dc0eed46a1af4bc5c148c5a3a3", request.Headers.GetValues("X-InventoryTracking-Session").Single());
        Assert.True(Assert.Single(results).Accepted);
    }

    [Fact]
    public async Task A_404_is_an_unknown_session_and_a_connection_failure_is_a_network_problem()
    {
        var gone = new SessionClient("https://host.example/", new Handler(_ => Json(HttpStatusCode.NotFound, "{\"error\":\"This session does not exist\"}")));
        var down = new SessionClient("https://host.example/", new Handler(_ => throw new HttpRequestException("no route")));

        var unknown = await Assert.ThrowsAsync<SessionException>(() => gone.CheckSessionAsync("fa9ed8dc0eed46a1af4bc5c148c5a3a3", CancellationToken.None));
        var network = await Assert.ThrowsAsync<SessionException>(() => down.CheckSessionAsync("fa9ed8dc0eed46a1af4bc5c148c5a3a3", CancellationToken.None));

        Assert.Equal(SessionProblem.Unknown, unknown.Problem);
        Assert.Equal(SessionProblem.Network, network.Problem);
    }

    [Fact]
    public async Task Create_returns_the_new_session_id()
    {
        var client = new SessionClient("https://host.example/", new Handler(_ => Json(HttpStatusCode.OK, "{\"id\":\"fa9ed8dc0eed46a1af4bc5c148c5a3a3\",\"files\":[]}")));

        Assert.Equal("fa9ed8dc0eed46a1af4bc5c148c5a3a3", await client.CreateSessionAsync(CancellationToken.None));
    }
}
