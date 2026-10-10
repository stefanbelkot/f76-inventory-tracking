namespace InventoryTracking.Syncer;

public interface ILog
{
    void Write(string message);
}

public sealed class NullLog : ILog
{
    public void Write(string message)
    {
    }
}

/// <summary>A plain-text log of at most about 1 MB (one older file is kept). Never throws.</summary>
public sealed class FileLog(string directory) : ILog
{
    private const long MaxBytes = 1_000_000;
    private readonly object _gate = new();
    private readonly string _file = Path.Combine(directory, "syncer.log");

    public void Write(string message)
    {
        try
        {
            lock (_gate)
            {
                Directory.CreateDirectory(directory);
                if (File.Exists(_file) && new FileInfo(_file).Length > MaxBytes)
                {
                    File.Move(_file, _file + ".old", overwrite: true);
                }

                File.AppendAllText(_file, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {message}{Environment.NewLine}");
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Logging must never disturb the tool.
        }
    }
}
