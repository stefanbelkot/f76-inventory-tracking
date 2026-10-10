using InventoryTracking.Core.Sources;

namespace InventoryTracking.Syncer.Sync;

/// <summary>Reads the export files directly inside the given folders (no subfolders). The newest copy of a name wins.</summary>
public sealed class FolderScanner(long maxFileBytes = 262_144) : IFolderScanner
{
    public ScanResult Scan(IReadOnlyList<string> folders)
    {
        var files = new Dictionary<string, LocalFile>(StringComparer.OrdinalIgnoreCase);
        var present = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var locked = new List<string>();
        var oversized = new List<string>();
        var allAvailable = true;

        foreach (var folder in folders)
        {
            string[] paths;
            try
            {
                if (!Directory.Exists(folder))
                {
                    allAvailable = false;
                    continue;
                }

                paths = Directory.GetFiles(folder, ExportFileName.SearchPattern);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                allAvailable = false;
                continue;
            }

            foreach (var path in paths)
            {
                var name = Path.GetFileName(path);
                if (!ExportFileName.IsExportFile(name))
                {
                    continue;
                }

                present.Add(name);
                try
                {
                    var info = new FileInfo(path);
                    if (info.Length > maxFileBytes)
                    {
                        oversized.Add(name);
                        continue;
                    }

                    using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                    using var buffer = new MemoryStream((int)Math.Max(info.Length, 16));
                    stream.CopyTo(buffer);
                    var content = buffer.ToArray();
                    var file = new LocalFile(name, path, info.LastWriteTimeUtc, content, LocalFile.HashOf(content));
                    if (!files.TryGetValue(name, out var other) || file.LastWriteUtc > other.LastWriteUtc)
                    {
                        files[name] = file;
                    }
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    locked.Add(name);
                }
            }
        }

        // A name that could be read from one folder is not "locked" just because another copy was.
        var stillLocked = locked.Where(name => !files.ContainsKey(name)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        return new ScanResult([.. files.Values], present, stillLocked, oversized, allAvailable);
    }
}
