namespace InventoryTracking.Server.Shell;

public sealed class MessageBoxNotifier : IUserNotifier
{
    public void ShowError(string message) =>
        MessageBox.Show(message, "Inventory Tracking Viewer", MessageBoxButtons.OK, MessageBoxIcon.Error);
}
