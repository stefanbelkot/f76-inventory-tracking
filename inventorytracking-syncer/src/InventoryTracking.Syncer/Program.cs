using InventoryTracking.Syncer.Settings;
using InventoryTracking.Syncer.Shell;
using InventoryTracking.Syncer.Startup;
using InventoryTracking.Syncer.Sync;

namespace InventoryTracking.Syncer;

internal static class Program
{
    private const string MutexName = @"Local\InventoryTrackingSyncer";
    private const string ShowEventName = @"Local\InventoryTrackingSyncer.Show";

    [STAThread]
    private static void Main(string[] args)
    {
        using var mutex = new Mutex(initiallyOwned: true, MutexName, out var first);
        if (!first)
        {
            // A second start asks the running instance to show its settings window.
            try
            {
                using var show = EventWaitHandle.OpenExisting(ShowEventName);
                show.Set();
            }
            catch (Exception ex) when (ex is WaitHandleCannotBeOpenedException or UnauthorizedAccessException)
            {
                // The running instance is still starting; nothing to do.
            }

            return;
        }

        string? Option(string name) => args.SkipWhile(a => a != name).Skip(1).FirstOrDefault();

        var directory = Option("--settings-dir") ?? SettingsStore.DefaultDirectory;
        var store = new SettingsStore(directory);
        if (Option("--server") is { } server && SessionLink.NormalizeServer(server) is { } normalized)
        {
            store.Update(s => s.Server = normalized);
        }

        var log = new FileLog(directory);
        log.Write("start");
        var exe = Environment.ProcessPath ?? Application.ExecutablePath;
        var autostart = new Autostart(new RegistryRunKey(), exe);
        autostart.Reconcile(store.Read(s => s.StartWithWindows));

        var engine = new SyncEngine(store, address => new SessionClient(address), new FolderScanner(), log);
        using var runner = new SyncRunner(store, engine, log);

        ApplicationConfiguration.Initialize();
        using var tray = new TrayApplication(store, engine, runner, autostart, log, background: args.Contains("--background"));
        using var showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEventName);
        var watcher = new Thread(() =>
        {
            while (showEvent.WaitOne())
            {
                tray.ShowSettingsFromOutside();
            }
        })
        { IsBackground = true };
        watcher.Start();
        Application.Run(tray);
        log.Write("exit");
    }
}
