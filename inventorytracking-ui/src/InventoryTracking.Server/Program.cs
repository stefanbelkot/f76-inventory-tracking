using InventoryTracking.Server;
using InventoryTracking.Server.Hosting;
using InventoryTracking.Server.Machine;
using InventoryTracking.Server.Shell;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        CommandLineOptions options;
        try
        {
            options = CommandLineOptions.Parse(args);
        }
        catch (ArgumentException ex)
        {
            new MessageBoxNotifier().ShowError(ex.Message);
            return 2;
        }

        IUserNotifier notifier = options.NoTray ? new StandardErrorNotifier() : new MessageBoxNotifier();
        return new ViewerApplication(new DefaultBrowserLauncher(), notifier, new WindowsMachine()).Run(options);
    }
}
