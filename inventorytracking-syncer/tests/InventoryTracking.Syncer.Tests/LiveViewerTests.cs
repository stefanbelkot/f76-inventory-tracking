using System.Net.Http.Json;
using System.Text.Json;
using InventoryTracking.Syncer.Settings;
using InventoryTracking.Syncer.Sync;

namespace InventoryTracking.Syncer.Tests;

/// <summary>
/// Runs the real client against a real viewer. Skipped unless INVENTORY_VIEWER_URL names a viewer in web (or both) mode,
/// for example: <c>InventoryTrackingViewer.exe --mode web --no-browser --no-tray --port 5099</c> and
/// <c>INVENTORY_VIEWER_URL=http://127.0.0.1:5099/</c>. INVENTORY_FIXTURES may name a folder with itemdump_*.json files.
/// </summary>
public class LiveViewerTests
{
    [Fact]
    public async Task Create_session_upload_check_and_mirror_a_deletion()
    {
        var url = Environment.GetEnvironmentVariable("INVENTORY_VIEWER_URL");
        if (string.IsNullOrEmpty(url))
        {
            return;
        }

        var server = SessionLink.NormalizeServer(url)!;
        using var dir = new TempDir();
        var folder = dir.Sub("modsdata");
        var fixtures = Environment.GetEnvironmentVariable("INVENTORY_FIXTURES");
        if (!string.IsNullOrEmpty(fixtures))
        {
            foreach (var file in Directory.GetFiles(fixtures, "itemdump_*.json").Take(5))
            {
                File.Copy(file, Path.Combine(folder, Path.GetFileName(file)));
            }
        }

        if (Directory.GetFiles(folder).Length == 0)
        {
            File.WriteAllText(Path.Combine(folder, "itemdump_acc_hero_stash_1.json"), "{\"v\":1,\"data\":[]}");
            File.WriteAllText(Path.Combine(folder, "itemdump_acc_hero_aid_1.json"), "{\"v\":1,\"data\":[]}");
        }

        var client = new SessionClient(server);
        var session = await client.CreateSessionAsync(CancellationToken.None);
        await client.CheckSessionAsync(session, CancellationToken.None);
        var store = new SettingsStore(Path.Combine(dir.Path, "settings"));
        store.Update(s =>
        {
            s.Server = server;
            s.Session = session;
            s.Folders = [new WatchedFolder { Path = folder }];
        });
        var engine = new SyncEngine(store, address => new SessionClient(address), new FolderScanner(), new ListLog());
        var expected = Directory.GetFiles(folder).Length;

        var first = await engine.RunOnceAsync(CancellationToken.None);
        var second = await engine.RunOnceAsync(CancellationToken.None);

        Assert.True(first.Uploaded > 0, first.Message);
        Assert.Equal(0, second.Uploaded);
        Assert.Equal(expected, await RemoteFileCount(server, session));

        File.Delete(Directory.GetFiles(folder)[0]);
        var third = await engine.RunOnceAsync(CancellationToken.None);

        Assert.Equal(1, third.Deleted);
        Assert.Equal(expected - 1, await RemoteFileCount(server, session));
    }

    private static async Task<int> RemoteFileCount(string server, string session)
    {
        using var http = new HttpClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(new Uri(server), "api/v1/session"));
        request.Headers.Add("X-InventoryTracking", "1");
        request.Headers.Add("X-InventoryTracking-Session", session);
        using var response = await http.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        return json.GetProperty("files").GetArrayLength();
    }
}
