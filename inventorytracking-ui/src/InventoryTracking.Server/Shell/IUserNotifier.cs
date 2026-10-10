namespace InventoryTracking.Server.Shell;

/// <summary>Shows a problem to the user. The viewer has no console window, so the normal way is a message box.</summary>
public interface IUserNotifier
{
    void ShowError(string message);
}
