namespace InventoryTracking.Server.Shell;

/// <summary>Used with <c>--no-tray</c>: errors go to standard error instead of a message box, so scripted runs never block.</summary>
public sealed class StandardErrorNotifier : IUserNotifier
{
    public void ShowError(string message) => Console.Error.WriteLine(message);
}
