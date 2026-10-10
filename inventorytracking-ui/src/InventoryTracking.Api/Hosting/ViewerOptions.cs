namespace InventoryTracking.Server.Hosting;

/// <summary>
/// How the viewer gets its data. <see cref="Local"/>: folders on this computer (the Windows exe). <see cref="Web"/>: files
/// uploaded into sessions on a server. <see cref="Both"/>: both at once, for debugging; a session then also reads the local folders.
/// </summary>
public enum ViewerMode
{
    Local,
    Web,
    Both,
}

/// <summary>Limits and locations of the web (upload) side. The defaults suit export files, which are about 20 KB each.</summary>
public sealed record WebOptions
{
    /// <summary>
    /// The default folder name. It must never equal a segment of an API address (`/api/v1/sessions` exists), because the IIS
    /// web.config hides this segment from requests.
    /// </summary>
    public const string DefaultSessionsDirectory = "session-data";

    /// <summary>Where the session folders live; relative paths are resolved against the application folder.</summary>
    public string SessionsDirectory { get; init; } = DefaultSessionsDirectory;

    /// <summary>The folder with the files offered on the Guide page (mod zips, Syncer) and their <c>downloads.json</c>; relative to the application folder. It is not part of the build; the owner copies it next to the site.</summary>
    public string DownloadsDirectory { get; init; } = "downloads";

    public long MaxFileBytes { get; init; } = 256 * 1024;

    public int MaxFilesPerSession { get; init; } = 100;

    public long MaxSessionBytes { get; init; } = 5 * 1024 * 1024;

    /// <summary>The most sessions that may exist at once; creating more is refused until old ones expire.</summary>
    public int MaxSessions { get; init; } = 5000;

    /// <summary>A session whose memory copy was not used for this long is dropped from memory (it is read from disk again on demand).</summary>
    public TimeSpan MemoryIdle { get; init; } = TimeSpan.FromHours(1);

    /// <summary>A session folder that was not used for this long is deleted.</summary>
    public TimeSpan SessionLifetime { get; init; } = TimeSpan.FromDays(7);

    /// <summary>Host names the web version answers to; empty means any (the web server's binding then decides).</summary>
    public IReadOnlyList<string> AllowedHosts { get; init; } = [];
}

/// <summary>What a host decides about the viewer: the mode, what it can do, and the web limits.</summary>
public sealed record ViewerOptions
{
    public ViewerMode Mode { get; init; } = ViewerMode.Local;

    public WebOptions Web { get; init; } = new();

    /// <summary>True for the Windows exe, which only answers on the loopback address and may be asked to quit.</summary>
    public bool Loopback { get; init; } = true;

    public bool Shutdown { get; init; }

    /// <summary>Local folder access (setup, folder browsing, saved settings).</summary>
    public bool Folders => Mode != ViewerMode.Web;

    /// <summary>Sessions and uploads.</summary>
    public bool Sessions => Mode != ViewerMode.Local;

    /// <exception cref="ArgumentException">The text is not local, web or both.</exception>
    public static ViewerMode ParseMode(string text) => text.ToLowerInvariant() switch
    {
        "local" => ViewerMode.Local,
        "web" => ViewerMode.Web,
        "both" => ViewerMode.Both,
        _ => throw new ArgumentException($"'{text}' is not a mode (local, web or both)."),
    };
}
