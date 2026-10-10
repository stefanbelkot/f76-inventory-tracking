using InventoryTracking.Core.Decoding;

namespace InventoryTracking.Core.Sources;

/// <summary>
/// Reads <c>itemdump_*.json</c> files directly inside one folder, read-only. Sub-folders and symbolic links are never followed,
/// and a file is opened with sharing so a game write in progress does not block the read.
/// </summary>
public sealed class LocalFolderSource : IExportSource
{
    private readonly string _folder;
    private readonly FileSystemWatcher? _watcher;
    private readonly ChangeDebouncer _debouncer;

    /// <param name="watch">False for a folder that is reloaded explicitly (web sessions): no file system watcher is created.</param>
    public LocalFolderSource(string folder, TimeSpan? debounce = null, bool watch = true)
    {
        _folder = Path.GetFullPath(folder);
        _debouncer = new ChangeDebouncer(debounce ?? TimeSpan.FromMilliseconds(500), () => Changed?.Invoke());
        _watcher = watch ? CreateWatcher() : null;
    }

    public event Action? Changed;

    public string Location => _folder;

    public IReadOnlyList<SourceFile> ReadFiles()
    {
        if (!Directory.Exists(_folder))
        {
            throw new DirectoryNotFoundException($"The folder '{_folder}' does not exist.");
        }

        return Directory.EnumerateFiles(_folder, ExportFileName.SearchPattern, SearchOption.TopDirectoryOnly)
            .Where(path => ExportFileName.IsExportFile(Path.GetFileName(path)))
            .Order(StringComparer.OrdinalIgnoreCase)
            .Select(ReadFile)
            .ToList();
    }

    public void Dispose()
    {
        _watcher?.Dispose();
        _debouncer.Dispose();
    }

    private static SourceFile ReadFile(string path)
    {
        var name = Path.GetFileName(path);
        try
        {
            var info = new FileInfo(path);
            if (info.Attributes.HasFlag(FileAttributes.ReparsePoint))
            {
                return new SourceFile(name, info.LastWriteTimeUtc, null, "Skipped: the file is a link.");
            }

            if (info.Length > DecodeLimits.MaxFileBytes)
            {
                return new SourceFile(name, info.LastWriteTimeUtc, null, $"The file is larger than {DecodeLimits.MaxFileBytes} bytes.");
            }

            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var buffer = new MemoryStream((int)info.Length);
            stream.CopyTo(buffer);
            return new SourceFile(name, info.LastWriteTimeUtc, buffer.ToArray(), null);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new SourceFile(name, DateTime.UtcNow, null, $"The file cannot be read: {ex.Message}");
        }
    }

    private FileSystemWatcher? CreateWatcher()
    {
        if (!Directory.Exists(_folder))
        {
            return null;
        }

        var watcher = new FileSystemWatcher(_folder, ExportFileName.SearchPattern)
        {
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
        };
        watcher.Created += (_, _) => _debouncer.Trigger();
        watcher.Changed += (_, _) => _debouncer.Trigger();
        watcher.Deleted += (_, _) => _debouncer.Trigger();
        watcher.Renamed += (_, _) => _debouncer.Trigger();
        watcher.EnableRaisingEvents = true;
        return watcher;
    }
}
