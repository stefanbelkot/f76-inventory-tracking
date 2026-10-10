using InventoryTracking.Core.Sources;

namespace InventoryTracking.Core.Setup;

/// <summary>Finds the modsdata folder for a path that is either the game install folder or the modsdata folder itself.</summary>
public static class GameFolderResolver
{
    private const string ModsData = "modsdata";

    public static FolderResolution? Resolve(string? input)
    {
        var path = LocalPath.Normalize(input);
        if (path is null || !Directory.Exists(path))
        {
            return null;
        }

        if (string.Equals(Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar)), ModsData, StringComparison.OrdinalIgnoreCase))
        {
            return Create(path, FolderKind.ModsData);
        }

        var inside = new (string Relative, FolderKind Kind)[]
        {
            (Path.Combine("Data", ModsData), FolderKind.InstallFolder),
            (Path.Combine("Content", "Data", ModsData), FolderKind.ParentFolder),
            (ModsData, FolderKind.DataFolder),
        };
        foreach (var (relative, kind) in inside)
        {
            var candidate = Path.Combine(path, relative);
            if (Directory.Exists(candidate))
            {
                return Create(candidate, kind);
            }
        }

        return ExportFileName.CountIn(path) > 0 ? Create(path, FolderKind.ExportFolder) : null;
    }

    private static FolderResolution Create(string path, FolderKind kind) =>
        new(path, kind, ExportFileName.CountIn(path));
}
