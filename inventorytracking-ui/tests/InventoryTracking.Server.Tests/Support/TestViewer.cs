using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text.Json;
using InventoryTracking.Core.Tests.Support;
using InventoryTracking.Server.Hosting;
using InventoryTracking.Server.Machine;

namespace InventoryTracking.Server.Tests.Support;

/// <summary>A real viewer host on a free loopback port, with its own settings folder and a copy of the real exports.</summary>
public sealed class TestViewer : IAsyncDisposable
{
    private readonly ViewerHost _host;

    private TestViewer(ViewerHost host, ShutdownSignal shutdown, TempFolder scratch, string? dataFolder)
    {
        _host = host;
        Shutdown = shutdown;
        Scratch = scratch;
        DataFolder = dataFolder;
        Http = new HttpClient { BaseAddress = new Uri(host.Address) };
        Http.DefaultRequestHeaders.Add("X-InventoryTracking", "1");
    }

    public ShutdownSignal Shutdown { get; }

    public TempFolder Scratch { get; }

    public string? DataFolder { get; }

    public string SettingsFolder => Scratch.Combine("settings");

    public HttpClient Http { get; }

    public int Port => _host.Port;

    /// <summary>
    /// A port below the dynamic range whose next <paramref name="followingFree"/> ports are free as well,
    /// so port fallback tests are not disturbed by outgoing connections.
    /// </summary>
    public static int FreePort(int followingFree = 0)
    {
        while (true)
        {
            var port = Random.Shared.Next(20000, 30000);
            if (Enumerable.Range(port, followingFree + 1).All(IsFree))
            {
                return port;
            }
        }
    }

    private static bool IsFree(int port)
    {
        try
        {
            var listener = new TcpListener(IPAddress.Loopback, port);
            listener.Start();
            listener.Stop();
            return true;
        }
        catch (SocketException)
        {
            return false;
        }
    }

    /// <param name="withData">Copy the five real exports into a scratch data folder and use it.</param>
    /// <param name="mode">Local folders, web sessions, or both. Sessions live in the scratch folder.</param>
    public static async Task<TestViewer> StartAsync(bool withData = true, int? startPort = null, ILocalMachine? machine = null, ViewerMode mode = ViewerMode.Local, Func<string, string>? sessionsDirectory = null)
    {
        var scratch = new TempFolder();
        string? dataFolder = null;
        if (withData)
        {
            dataFolder = scratch.Combine("modsdata");
            Directory.CreateDirectory(dataFolder);
            foreach (var file in Directory.GetFiles(Fixtures.RealFolder))
            {
                File.Copy(file, Path.Combine(dataFolder, Path.GetFileName(file)));
            }
        }

        var options = new CommandLineOptions
        {
            Folders = dataFolder is null ? [] : [dataFolder],
            SettingsDirectory = scratch.Combine("settings"),
            Mode = mode,
            SessionsDirectory = sessionsDirectory?.Invoke(scratch.Path) ?? scratch.Combine("sessions"),
            StartPort = startPort ?? FreePort(),
            NoBrowser = true,
            NoTray = true,
        };
        var shutdown = new ShutdownSignal();
        try
        {
            var host = await ViewerHost.StartAsync(options, shutdown, machine ?? new FakeMachine(null));
            return new TestViewer(host, shutdown, scratch, dataFolder);
        }
        catch
        {
            scratch.Dispose();
            throw;
        }
    }

    public string SessionsFolder => Scratch.Combine("sessions");

    /// <summary>Creates a session and makes it the one this viewer's HTTP client names in its calls.</summary>
    public async Task<string> NewSessionAsync()
    {
        using var response = await Http.PostAsync("/api/v1/sessions", null);
        response.EnsureSuccessStatusCode();
        var id = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;
        UseSession(id);
        return id;
    }

    public void UseSession(string? id)
    {
        Http.DefaultRequestHeaders.Remove("X-InventoryTracking-Session");
        if (id is not null)
        {
            Http.DefaultRequestHeaders.Add("X-InventoryTracking-Session", id);
        }
    }

    /// <summary>Uploads files (name and bytes) as one multipart request into the current session.</summary>
    public async Task<HttpResponseMessage> UploadAsync(params (string Name, byte[] Content)[] files)
    {
        using var form = new MultipartFormDataContent();
        foreach (var (name, content) in files)
        {
            form.Add(new ByteArrayContent(content), "files", name);
        }

        return await Http.PostAsync("/api/v1/session/files", form);
    }

    public async Task<JsonElement> GetJsonAsync(string path)
    {
        using var response = await Http.GetAsync(path);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    public async ValueTask DisposeAsync()
    {
        Http.Dispose();
        await _host.DisposeAsync();
        Scratch.Dispose();
    }
}
