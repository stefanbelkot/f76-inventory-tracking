using InventoryTracking.Core.Sources;

namespace InventoryTracking.Core.Setup;

public sealed record DirectoryEntry(string Name, string Path, bool HasExports);

/// <summary>The sub-folders of a path. <paramref name="Path"/> is empty for the list of drives.</summary>
public sealed record DirectoryListing(string Path, string? Parent, IReadOnlyList<DirectoryEntry> Dirs);

/// <summary>Lists folder names only, for the folder picker of the setup page. Files are never listed or read.</summary>
public static class DirectoryBrowser
{
    /// <returns>The listing, or null when the path is not an absolute local folder that can be listed.</returns>
    public static DirectoryListing? List(string? path, IEnumerable<string> driveRoots)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            var drives = driveRoots.Select(root => new DirectoryEntry(root, root, ExportFileName.CountIn(root) > 0)).ToList();
            return new DirectoryListing("", null, drives);
        }

        var full = LocalPath.Normalize(path);
        if (full is null || !Directory.Exists(full))
        {
            return null;
        }

        try
        {
            var dirs = new DirectoryInfo(full).EnumerateDirectories()
                .Where(dir => !dir.Attributes.HasFlag(FileAttributes.Hidden) && !dir.Attributes.HasFlag(FileAttributes.System))
                .Where(dir => !dir.Attributes.HasFlag(FileAttributes.ReparsePoint))
                .OrderBy(dir => dir.Name, StringComparer.OrdinalIgnoreCase)
                .Select(dir => new DirectoryEntry(dir.Name, dir.FullName, ExportFileName.CountIn(dir.FullName) > 0))
                .ToList();
            var parent = Directory.GetParent(full)?.FullName ?? "";
            return new DirectoryListing(full, parent, dirs);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
