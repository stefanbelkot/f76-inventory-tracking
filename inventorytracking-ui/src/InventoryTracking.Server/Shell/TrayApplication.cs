using InventoryTracking.Server.Hosting;

namespace InventoryTracking.Server.Shell;

/// <summary>
/// The notification-area icon with the menu "Open in browser" and "Quit". <see cref="Run"/> is the message loop
/// of the calling (STA) thread and returns when shutdown has been requested.
/// </summary>
public static class TrayApplication
{
    private const int MaxTooltipLength = 63;

    public static void Run(string address, IBrowserLauncher browser, ShutdownSignal shutdown)
    {
        using var marshal = new Control();
        _ = marshal.Handle;
        using var context = new ApplicationContext();
        using var icon = TrayIconFactory.Create();
        using var menu = new ContextMenuStrip();
        menu.Items.Add("Open in browser", null, (_, _) => browser.Open(address));
        menu.Items.Add("Quit", null, (_, _) => shutdown.Request());

        using var notify = new NotifyIcon
        {
            Icon = icon,
            Text = Tooltip(address),
            ContextMenuStrip = menu,
            Visible = true,
        };
        notify.DoubleClick += (_, _) => browser.Open(address);

        _ = shutdown.Requested.ContinueWith(
            _ => marshal.BeginInvoke(context.ExitThread),
            TaskScheduler.Default);
        Application.Run(context);
        notify.Visible = false;
    }

    private static string Tooltip(string address)
    {
        var text = $"Inventory Tracking Viewer - {address}";
        return text.Length <= MaxTooltipLength ? text : text[..MaxTooltipLength];
    }
}
