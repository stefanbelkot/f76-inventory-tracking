using System.Diagnostics;
using System.Security.Principal;

namespace InventoryTracking.Server.Instance;

/// <summary>The per-user names of the single-instance mutex and the pipe used to reach the running instance.</summary>
public sealed record InstanceNames(string Mutex, string Pipe)
{
    public static InstanceNames For(string instanceId)
    {
        var user = WindowsIdentity.GetCurrent().User?.Value ?? Environment.UserName;
        var session = Process.GetCurrentProcess().SessionId;
        var suffix = $"{user}.{session}.{instanceId}";
        return new InstanceNames($"InventoryTrackingViewer.{suffix}", $"InventoryTrackingViewer.{suffix}.pipe");
    }
}
