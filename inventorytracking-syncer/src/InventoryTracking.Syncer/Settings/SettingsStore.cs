using System.Text.Json;

namespace InventoryTracking.Syncer.Settings;

/// <summary>
/// Loads and saves <c>settings.json</c> in a private folder. Writes go through a temporary file; a damaged file is kept as
/// <c>settings.json.bad</c> and the program starts with empty settings. <see cref="Current"/> is shared by the UI and the sync thread.
/// </summary>
public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private readonly object _gate = new();
    private readonly string _file;

    public SettingsStore(string directory)
    {
        Directory = directory;
        _file = Path.Combine(directory, "settings.json");
        Current = Load();
    }

    public string Directory { get; }

    public SyncerSettings Current { get; private set; }

    public bool Existed { get; private set; }

    public static string DefaultDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "InventoryTrackingSyncer");

    private SyncerSettings Load()
    {
        if (!File.Exists(_file))
        {
            return new SyncerSettings();
        }

        try
        {
            var loaded = JsonSerializer.Deserialize<SyncerSettings>(File.ReadAllText(_file), Json);
            if (loaded is not null)
            {
                Existed = true;
                loaded.Folders ??= [];
                loaded.Uploaded ??= [];
                return loaded;
            }
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            // Fall through: keep the damaged file for the owner and start fresh.
        }

        try
        {
            File.Move(_file, _file + ".bad", overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Not being able to keep a copy must not stop the program.
        }

        return new SyncerSettings();
    }

    /// <summary>Changes the settings under a lock and saves them.</summary>
    public void Update(Action<SyncerSettings> change)
    {
        lock (_gate)
        {
            change(Current);
            Save();
        }
    }

    public T Read<T>(Func<SyncerSettings, T> read)
    {
        lock (_gate)
        {
            return read(Current);
        }
    }

    private void Save()
    {
        System.IO.Directory.CreateDirectory(Directory);
        var temporary = _file + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(Current, Json));
        File.Move(temporary, _file, overwrite: true);
        Existed = true;
    }
}
