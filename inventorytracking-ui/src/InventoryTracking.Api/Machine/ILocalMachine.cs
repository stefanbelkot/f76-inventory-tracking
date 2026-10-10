namespace InventoryTracking.Server.Machine;

/// <summary>What the viewer needs to know about this computer to find the game.</summary>
public interface ILocalMachine
{
    /// <summary>The Steam install folder, or null when Steam is not installed.</summary>
    string? SteamPath { get; }

    /// <summary>The root folders of the fixed drives, for example <c>C:\</c>.</summary>
    IReadOnlyList<string> FixedDriveRoots { get; }
}
