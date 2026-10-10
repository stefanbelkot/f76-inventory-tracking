using InventoryTracking.Core.Catalog;
using InventoryTracking.Core.Setup;
using InventoryTracking.Core.Sources;

namespace InventoryTracking.Server.Settings;

public enum FolderOrigin
{
    Saved,
    CommandLine,
}

/// <summary>
/// The export folders that are read together: where the list came from, changing it, and attaching it to the catalog.
/// A folder is stored once, compared case-insensitively.
/// </summary>
public sealed class FolderSession(ExportCatalogService catalog, SettingsStore settings, IReadOnlyList<string> commandLineFolders)
{
    private readonly object _gate = new();

    public IReadOnlyList<string> Folders { get; private set; } = [];

    public FolderOrigin Origin { get; private set; } = FolderOrigin.Saved;

    /// <summary>Changes every time the folder list is attached again; sessions use it to notice that the local folders changed.</summary>
    public int Generation { get; private set; }

    /// <summary>Loads the folders given on the command line, or else the saved ones. Without either, setup is needed.</summary>
    public void Start()
    {
        lock (_gate)
        {
            if (commandLineFolders.Count > 0)
            {
                Attach(Distinct(commandLineFolders), FolderOrigin.CommandLine);
                return;
            }

            var saved = settings.Load().Folders;
            if (saved.Count > 0)
            {
                Attach(Distinct(saved), FolderOrigin.Saved);
            }
        }
    }

    /// <summary>Adds a folder to the saved list (a no-op when it is already in it) and starts reading it as well.</summary>
    public void Add(string folder)
    {
        lock (_gate)
        {
            Replace(Distinct([.. Folders, folder]));
        }
    }

    /// <summary>Removes a folder from the saved list; with none left, setup is needed again.</summary>
    public void Remove(string folder)
    {
        lock (_gate)
        {
            var key = LocalPath.Normalize(folder) ?? folder;
            Replace(Folders.Where(existing => !Same(existing, key)).ToList());
        }
    }

    private void Replace(IReadOnlyList<string> folders)
    {
        settings.Update(current => current with { Folders = folders, Folder = null });
        Attach(folders, FolderOrigin.Saved);
    }

    private void Attach(IReadOnlyList<string> folders, FolderOrigin origin)
    {
        Folders = folders;
        Origin = origin;
        Generation++;
        catalog.Attach(folders.Count == 0
            ? null
            : new CompositeExportSource(folders.Select(folder => new LocalFolderSource(folder))));
    }

    private static List<string> Distinct(IEnumerable<string> folders)
    {
        var result = new List<string>();
        foreach (var folder in folders)
        {
            var normalized = LocalPath.Normalize(folder) ?? folder;
            if (!result.Any(existing => Same(existing, normalized)))
            {
                result.Add(normalized);
            }
        }

        return result;
    }

    private static bool Same(string left, string right) =>
        string.Equals(left.TrimEnd('\\', '/'), right.TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase);
}
