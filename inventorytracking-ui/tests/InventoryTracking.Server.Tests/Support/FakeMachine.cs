using InventoryTracking.Server.Machine;

namespace InventoryTracking.Server.Tests.Support;

public sealed class FakeMachine(string? steamPath, params string[] driveRoots) : ILocalMachine
{
    public string? SteamPath => steamPath;

    public IReadOnlyList<string> FixedDriveRoots => driveRoots;
}
