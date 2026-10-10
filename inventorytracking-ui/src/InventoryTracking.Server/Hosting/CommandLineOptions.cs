namespace InventoryTracking.Server.Hosting;

/// <summary>
/// The command line. Besides the normal start, a few flags let automated runs avoid disturbing the user:
/// <c>--no-browser</c>, <c>--no-tray</c>, <c>--folder</c>, <c>--settings-dir</c>, <c>--port</c>, <c>--instance-id</c>,
/// <c>--mode</c> (local, web or both) and <c>--sessions-dir</c>.
/// </summary>
public sealed record CommandLineOptions
{
    public const int DefaultPort = 8080;

    public bool NoBrowser { get; init; }

    /// <summary>No tray icon and no message boxes; the process runs until shutdown is requested.</summary>
    public bool NoTray { get; init; }

    /// <summary>Modsdata folders for this run only (repeat <c>--folder</c> for several); they are not saved to the settings.</summary>
    public IReadOnlyList<string> Folders { get; init; } = [];

    public string SettingsDirectory { get; init; } = Settings.SettingsStore.DefaultDirectory;

    /// <summary>Local folders only (default), uploaded files in sessions only (web), or both for debugging.</summary>
    public ViewerMode Mode { get; init; } = ViewerMode.Local;

    /// <summary>Where web sessions are kept; by default a <c>sessions</c> folder next to <c>settings.json</c>.</summary>
    public string? SessionsDirectory { get; init; }

    /// <summary>The first port to try; the next ones follow when it is taken.</summary>
    public int StartPort { get; init; } = DefaultPort;

    /// <summary>Distinguishes instances for the single-instance lock; tests use their own.</summary>
    public string InstanceId { get; init; } = "default";

    /// <exception cref="ArgumentException">An argument is unknown or has no valid value.</exception>
    public static CommandLineOptions Parse(IReadOnlyList<string> args)
    {
        var options = new CommandLineOptions();
        for (var i = 0; i < args.Count; i++)
        {
            options = args[i] switch
            {
                "--no-browser" => options with { NoBrowser = true },
                "--no-tray" => options with { NoTray = true },
                "--folder" => options with { Folders = [.. options.Folders, Value(args, ref i)] },
                "--settings-dir" => options with { SettingsDirectory = Value(args, ref i) },
                "--mode" => options with { Mode = ViewerOptions.ParseMode(Value(args, ref i)) },
                "--sessions-dir" => options with { SessionsDirectory = Value(args, ref i) },
                "--instance-id" => options with { InstanceId = Value(args, ref i) },
                "--port" => options with { StartPort = ParsePort(Value(args, ref i)) },
                var unknown => throw new ArgumentException($"Unknown argument '{unknown}'."),
            };
        }

        return options;
    }

    private static string Value(IReadOnlyList<string> args, ref int index) =>
        ++index < args.Count ? args[index] : throw new ArgumentException($"The argument '{args[index - 1]}' needs a value.");

    private static int ParsePort(string text) =>
        int.TryParse(text, out var port) && port is >= 1 and <= 65535
            ? port
            : throw new ArgumentException($"'{text}' is not a valid port.");
}
