using System.Text.Json.Serialization;

namespace InventoryTracking.Server.Settings;

/// <summary>Everything the viewer saves: the chosen folders and a few interface preferences. Never inventory data.</summary>
public sealed record ViewerSettings
{
    /// <summary>The export folders that are read together (Steam, Xbox and any the user added). Empty means setup is needed.</summary>
    public IReadOnlyList<string> Folders { get; init; } = [];

    /// <summary>The single folder of earlier versions; only read to migrate it into <see cref="Folders"/>.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Folder { get; init; }

    /// <summary>"system", "light" or "dark".</summary>
    public string Theme { get; init; } = "system";

    /// <summary>The visible item table columns in order; null means the default layout.</summary>
    public IReadOnlyList<string>? Columns { get; init; }
}
