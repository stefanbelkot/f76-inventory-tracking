namespace InventoryTracking.Server.Hosting;

/// <summary>Raised once when the user asks the viewer to quit (tray menu or the page's Quit button).</summary>
public sealed class ShutdownSignal
{
    private readonly TaskCompletionSource _requested = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task Requested => _requested.Task;

    public void Request() => _requested.TrySetResult();
}
