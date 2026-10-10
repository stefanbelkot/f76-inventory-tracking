using InventoryTracking.Server.Hosting;
using InventoryTracking.Server.Instance;
using InventoryTracking.Server.Machine;
using InventoryTracking.Server.Shell;

namespace InventoryTracking.Server;

/// <summary>
/// The program flow: parse the command line, hold the single-instance lock (or ask the running instance to open the
/// browser), start the web server, then live in the tray until a quit is requested.
/// </summary>
public sealed class ViewerApplication(
    IBrowserLauncher browser,
    IUserNotifier notifier,
    ILocalMachine machine,
    TimeSpan? runningInstanceTimeout = null)
{
    private readonly TimeSpan _runningInstanceTimeout = runningInstanceTimeout ?? TimeSpan.FromSeconds(5);

    public int Run(CommandLineOptions options)
    {
        var names = InstanceNames.For(options.InstanceId);
        using var guard = SingleInstanceGuard.TryAcquire(names);
        return guard is null ? AskRunningInstanceToOpen(names) : RunFirstInstance(options, names);
    }

    private int AskRunningInstanceToOpen(InstanceNames names)
    {
        if (InstancePipeClient.RequestOpenAsync(names.Pipe, _runningInstanceTimeout).GetAwaiter().GetResult())
        {
            return 0;
        }

        notifier.ShowError("The viewer is already running, but it did not answer. Quit it from its tray icon and start it again.");
        return 1;
    }

    private int RunFirstInstance(CommandLineOptions options, InstanceNames names)
    {
        var shutdown = new ShutdownSignal();
        ViewerHost host;
        try
        {
            host = Task.Run(() => ViewerHost.StartAsync(options, shutdown, machine)).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            notifier.ShowError($"The viewer could not start: {ex.Message}");
            return 1;
        }

        void OpenBrowser()
        {
            if (!options.NoBrowser)
            {
                browser.Open(host.Address);
            }
        }

        using (var pipe = new InstancePipeServer(names.Pipe, OpenBrowser))
        {
            OpenBrowser();

            if (options.NoTray)
            {
                shutdown.Requested.GetAwaiter().GetResult();
            }
            else
            {
                TrayApplication.Run(host.Address, browser, shutdown);
            }
        }

        host.DisposeAsync().AsTask().GetAwaiter().GetResult();
        return 0;
    }
}
