using Microsoft.Win32;

namespace InventoryTracking.Server.Machine;

/// <summary>Reads the Steam path from the registry and lists the fixed drives.</summary>
public sealed class WindowsMachine : ILocalMachine
{
    public string? SteamPath
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
            return key?.GetValue("SteamPath") is string path && path.Length > 0 ? Path.GetFullPath(path) : null;
        }
    }

    public IReadOnlyList<string> FixedDriveRoots =>
        DriveInfo.GetDrives()
            .Where(drive => drive.DriveType == DriveType.Fixed && drive.IsReady)
            .Select(drive => drive.RootDirectory.FullName)
            .ToList();
}
