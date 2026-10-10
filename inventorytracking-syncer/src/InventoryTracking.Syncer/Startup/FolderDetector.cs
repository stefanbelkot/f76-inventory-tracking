using InventoryTracking.Core.Setup;
using InventoryTracking.Syncer.Settings;
using Microsoft.Win32;

namespace InventoryTracking.Syncer.Startup;

/// <summary>Finds Steam and Xbox / Game Pass Fallout 76 export folders with the viewer's detection logic.</summary>
public static class FolderDetector
{
    public static IReadOnlyList<WatchedFolder> Detect()
    {
        var drives = DriveInfo.GetDrives()
            .Where(drive => drive.DriveType == DriveType.Fixed && drive.IsReady)
            .Select(drive => drive.RootDirectory.FullName)
            .ToList();
        return ModsDataCandidates.Find(SteamPath(), drives)
            .Select(candidate => new WatchedFolder
            {
                Path = candidate.Path,
                Enabled = true,
                Origin = candidate.Label.StartsWith("Steam", StringComparison.OrdinalIgnoreCase) ? "steam" : "xbox",
            })
            .ToList();
    }

    /// <summary>Adds newly found folders; existing entries (and the user's choice to disable one) stay untouched. Returns the added ones.</summary>
    public static List<WatchedFolder> Merge(List<WatchedFolder> current, IEnumerable<WatchedFolder> found)
    {
        var added = new List<WatchedFolder>();
        foreach (var folder in found)
        {
            if (!current.Any(existing => string.Equals(existing.Path.TrimEnd('\\'), folder.Path.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase)))
            {
                current.Add(folder);
                added.Add(folder);
            }
        }

        return added;
    }

    private static string? SteamPath()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
            return key?.GetValue("SteamPath") is string path && path.Length > 0 ? Path.GetFullPath(path) : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            return null;
        }
    }
}
