using System.Text.Json;

namespace InventoryTracking.Server.Settings;

/// <summary>Reads and writes <c>settings.json</c>. This is the only file the viewer ever writes.</summary>
public sealed class SettingsStore(string directory)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    private readonly object _gate = new();
    private readonly string _path = Path.Combine(directory, "settings.json");

    public static string DefaultDirectory { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "InventoryTrackingViewer");

    /// <summary>The saved settings, or the defaults when the file is missing or unreadable.</summary>
    public ViewerSettings Load()
    {
        lock (_gate)
        {
            return LoadLocked();
        }
    }

    public ViewerSettings Update(Func<ViewerSettings, ViewerSettings> change)
    {
        lock (_gate)
        {
            var updated = change(LoadLocked());
            Directory.CreateDirectory(directory);
            var temporary = _path + ".tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(updated, JsonOptions));
            File.Move(temporary, _path, overwrite: true);
            return updated;
        }
    }

    private ViewerSettings LoadLocked()
    {
        try
        {
            return File.Exists(_path)
                ? Migrate(JsonSerializer.Deserialize<ViewerSettings>(File.ReadAllText(_path), JsonOptions) ?? new ViewerSettings())
                : new ViewerSettings();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return new ViewerSettings();
        }
    }

    /// <summary>Settings saved by the first version hold one <c>folder</c>; it becomes the first entry of <c>folders</c>.</summary>
    private static ViewerSettings Migrate(ViewerSettings settings) =>
        settings.Folder is { } legacy
            ? settings with { Folder = null, Folders = settings.Folders.Count > 0 ? settings.Folders : [legacy] }
            : settings with { Folders = settings.Folders ?? [] };
}
