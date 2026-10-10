using System.Net;
using InventoryTracking.Core.Setup;
using InventoryTracking.Server.Machine;
using InventoryTracking.Server.Settings;

namespace InventoryTracking.Server.Hosting;

/// <summary>
/// The running web server of the Windows exe. It binds 127.0.0.1 only, starting at the requested port and counting up
/// when a port cannot be bound. What it serves (local folders, web sessions or both) comes from the shared
/// <see cref="ViewerRuntime"/>.
/// </summary>
public sealed class ViewerHost : IAsyncDisposable
{
    public const int MaxPortTries = 100;

    private readonly WebApplication _app;
    private readonly ViewerRuntime _runtime;

    private ViewerHost(WebApplication app, ViewerRuntime runtime, int port)
    {
        _app = app;
        _runtime = runtime;
        Port = port;
    }

    public int Port { get; }

    public string Address => $"http://127.0.0.1:{Port}/";

    /// <exception cref="InvalidOperationException">No port could be bound.</exception>
    public static async Task<ViewerHost> StartAsync(CommandLineOptions options, ShutdownSignal shutdown, ILocalMachine machine)
    {
        var sessionsDirectory = options.SessionsDirectory ?? Path.Combine(options.SettingsDirectory, "sessions");
        var runtime = ViewerRuntime.Create(
            new ViewerOptions
            {
                Mode = options.Mode,
                Loopback = true,
                Shutdown = true,
                Web = new WebOptions { SessionsDirectory = sessionsDirectory },
            },
            options.SettingsDirectory,
            options.Folders.Select(ResolveCommandLineFolder).ToList(),
            machine,
            shutdown);
        runtime.Start();

        var lastPort = Math.Min(options.StartPort + MaxPortTries - 1, 65535);
        for (var port = options.StartPort; port <= lastPort; port++)
        {
            var app = BuildApp(port, runtime);
            try
            {
                await app.StartAsync();
                return new ViewerHost(app, runtime, port);
            }
            catch (IOException)
            {
                await app.DisposeAsync();
            }
        }

        runtime.Dispose();
        throw new InvalidOperationException($"No free port was found between {options.StartPort} and {lastPort}.");
    }

    public async Task StopAsync()
    {
        await _app.StopAsync();
        _runtime.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        await _app.DisposeAsync();
    }

    private static string ResolveCommandLineFolder(string folder) => GameFolderResolver.Resolve(folder)?.Path ?? folder;

    private static WebApplication BuildApp(int port, ViewerRuntime runtime)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { ContentRootPath = AppContext.BaseDirectory });
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(kestrel =>
        {
            kestrel.AddServerHeader = false;
            kestrel.Listen(IPAddress.Loopback, port);
        });
        builder.Services.AddViewer(runtime);
        var app = builder.Build();
        app.UseViewer(runtime);
        return app;
    }
}
