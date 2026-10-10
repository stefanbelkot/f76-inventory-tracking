using System.Diagnostics;

namespace InventoryTracking.Server.Shell;

/// <summary>Opens an address in the user's default browser.</summary>
public sealed class DefaultBrowserLauncher : IBrowserLauncher
{
    public void Open(string address) =>
        Process.Start(new ProcessStartInfo(address) { UseShellExecute = true })?.Dispose();
}
