using System.Text.Json;
using System.Text.Json.Serialization;

namespace InventoryTracking.Syncer.Settings;

public sealed class WatchedFolder
{
    public string Path { get; set; } = "";
    public bool Enabled { get; set; } = true;

    /// <summary>"steam", "xbox" or "manual".</summary>
    public string Origin { get; set; } = "manual";
}

/// <summary>The saved configuration (<c>settings.json</c>). Unknown fields survive a load and save.</summary>
public sealed class SyncerSettings
{
    public int Version { get; set; } = 1;
    public string Server { get; set; } = SessionLink.DefaultServer;
    public string Session { get; set; } = "";
    public List<WatchedFolder> Folders { get; set; } = [];
    public bool StartWithWindows { get; set; }
    public bool AskedAboutAutostart { get; set; }
    public int RescanSeconds { get; set; } = 60;

    /// <summary>Per session: the content hash of every file this tool uploaded, by file name.</summary>
    public Dictionary<string, Dictionary<string, string>> Uploaded { get; set; } = [];

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; set; }

    public bool Configured => SessionLink.IsSessionId(Session) && Folders.Any(folder => folder.Enabled);

    public Dictionary<string, string> UploadedFor(string session)
    {
        if (!Uploaded.TryGetValue(session, out var map))
        {
            map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            Uploaded[session] = map;
        }

        return map;
    }
}
