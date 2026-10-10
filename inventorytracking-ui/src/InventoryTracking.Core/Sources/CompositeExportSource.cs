namespace InventoryTracking.Core.Sources;

/// <summary>
/// Reads several sources as one, for example the Steam and the Xbox modsdata folders plus folders the user copied in.
/// A source that cannot be listed (a folder on a drive that is not connected) is reported as one unreadable entry
/// and does not stop the others. A file that appears in several sources with identical content is returned once;
/// the same file name with different content is kept, so the catalog shows both snapshots.
/// </summary>
public sealed class CompositeExportSource : IExportSource
{
    private readonly IReadOnlyList<IExportSource> _sources;

    public CompositeExportSource(IEnumerable<IExportSource> sources)
    {
        _sources = [.. sources];
        foreach (var source in _sources)
        {
            source.Changed += OnSourceChanged;
        }
    }

    public event Action? Changed;

    public string Location => string.Join("; ", _sources.Select(source => source.Location));

    public IReadOnlyList<SourceFile> ReadFiles()
    {
        var files = new List<SourceFile>();
        var seen = new Dictionary<string, List<byte[]>>(StringComparer.OrdinalIgnoreCase);
        foreach (var source in _sources)
        {
            IReadOnlyList<SourceFile> read;
            try
            {
                read = source.ReadFiles();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                files.Add(new SourceFile(source.Location, DateTime.UtcNow, null, ex.Message, source.Location));
                continue;
            }

            foreach (var file in read)
            {
                var tagged = file with { Origin = source.Location };
                if (tagged.Content is not null && IsCopyOfSeen(seen, tagged))
                {
                    continue;
                }

                files.Add(tagged);
            }
        }

        return files;
    }

    public void Dispose()
    {
        foreach (var source in _sources)
        {
            source.Changed -= OnSourceChanged;
            source.Dispose();
        }
    }

    private void OnSourceChanged() => Changed?.Invoke();

    private static bool IsCopyOfSeen(Dictionary<string, List<byte[]>> seen, SourceFile file)
    {
        if (!seen.TryGetValue(file.Name, out var contents))
        {
            seen[file.Name] = [file.Content!];
            return false;
        }

        if (contents.Any(known => known.AsSpan().SequenceEqual(file.Content!)))
        {
            return true;
        }

        contents.Add(file.Content!);
        return false;
    }
}
