using InventoryTracking.Core.Catalog;
using InventoryTracking.Core.Types;
using InventoryTracking.Server.Machine;
using InventoryTracking.Server.Sessions;
using InventoryTracking.Server.Settings;

namespace InventoryTracking.Server.Hosting;

/// <summary>
/// The long-lived state of one running viewer, shared by every web application instance a host builds (the Windows exe
/// builds a new one for each port it tries): the local catalog and settings, the sessions, and the shutdown signal.
/// What exists depends on the mode: local folders in local and both mode, sessions in web and both mode.
/// </summary>
public sealed class ViewerRuntime : IDisposable
{
    private ViewerRuntime(ViewerOptions options)
    {
        Options = options;
        Catalogs = new CatalogAccess(this);
    }

    public ViewerOptions Options { get; }

    public CatalogAccess Catalogs { get; }

    // Local folders (local and both mode).
    public ExportCatalogService? LocalCatalog { get; private init; }

    public FolderSession? Folders { get; private init; }

    public SettingsStore? Settings { get; private init; }

    public ILocalMachine? Machine { get; private init; }

    public ShutdownSignal? Shutdown { get; private init; }

    // Sessions (web and both mode).
    public SessionStore? Sessions { get; private init; }

    public SessionCatalogs? SessionCatalogs { get; private init; }

    /// <summary>The folder served at <c>/downloads</c> (web and both mode); it may not exist yet.</summary>
    public string? DownloadsFolder { get; private init; }

    /// <param name="settingsDirectory">Where <c>settings.json</c> lives (local and both mode).</param>
    /// <param name="commandLineFolders">Folders for this run only, instead of the saved ones.</param>
    /// <param name="machine">Describes this computer for game detection; required when the mode has local folders.</param>
    /// <param name="applicationDirectory">Relative session folders are placed here.</param>
    public static ViewerRuntime Create(
        ViewerOptions options,
        string? settingsDirectory = null,
        IReadOnlyList<string>? commandLineFolders = null,
        ILocalMachine? machine = null,
        ShutdownSignal? shutdown = null,
        string? applicationDirectory = null,
        TimeProvider? time = null)
    {
        var types = ItemTypeTable.Default;
        ExportCatalogService? localCatalog = null;
        FolderSession? folders = null;
        SettingsStore? settings = null;
        if (options.Folders)
        {
            localCatalog = new ExportCatalogService(types);
            settings = new SettingsStore(settingsDirectory ?? SettingsStore.DefaultDirectory);
            folders = new FolderSession(localCatalog, settings, commandLineFolders ?? []);
        }

        SessionStore? sessions = null;
        SessionCatalogs? sessionCatalogs = null;
        if (options.Sessions)
        {
            var root = Path.IsPathRooted(options.Web.SessionsDirectory)
                ? options.Web.SessionsDirectory
                : Path.Combine(applicationDirectory ?? AppContext.BaseDirectory, options.Web.SessionsDirectory);
            sessions = new SessionStore(root, options.Web, time);
            sessionCatalogs = new SessionCatalogs(
                sessions,
                types,
                time,
                folders is null ? null : () => (folders.Generation, folders.Folders));
        }

        return new ViewerRuntime(options)
        {
            LocalCatalog = localCatalog,
            Folders = folders,
            Settings = settings,
            Machine = machine,
            Shutdown = options.Shutdown ? shutdown ?? new ShutdownSignal() : null,
            Sessions = sessions,
            SessionCatalogs = sessionCatalogs,
            DownloadsFolder = options.Sessions
                ? Path.IsPathRooted(options.Web.DownloadsDirectory)
                    ? options.Web.DownloadsDirectory
                    : Path.Combine(applicationDirectory ?? AppContext.BaseDirectory, options.Web.DownloadsDirectory)
                : null,
        };
    }

    /// <summary>Why the session folder cannot be used (set by <see cref="Start"/>), or null when it works or there are no sessions.</summary>
    public string? StorageProblem { get; private set; }

    /// <summary>Loads the local folders, checks that the session folder is writable and removes expired sessions.</summary>
    public void Start()
    {
        Folders?.Start();
        if (Sessions is null)
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(Sessions.Root);
            var probe = Path.Combine(Sessions.Root, $".write-test-{Guid.NewGuid():N}");
            File.WriteAllText(probe, "ok");
            File.Delete(probe);
            Sessions.DeleteExpired();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            StorageProblem = $"The session folder '{Sessions.Root}' cannot be written: {ex.Message}";
        }
    }

    /// <summary>Removes expired session folders and idle memory copies; hosts call this every few minutes.</summary>
    public void Maintain()
    {
        Sessions?.DeleteExpired();
        SessionCatalogs?.EvictIdle();
    }

    public void Dispose()
    {
        SessionCatalogs?.Dispose();
        LocalCatalog?.Dispose();
    }
}
