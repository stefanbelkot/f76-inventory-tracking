namespace InventoryTracking.Core.Sources;

/// <summary>Where export files come from: a local folder now, uploaded files in a hosted version.</summary>
public interface IExportSource : IDisposable
{
    /// <summary>Human-readable place the files come from (for example the folder path).</summary>
    string Location { get; }

    /// <summary>Raised (debounced) when the underlying files changed. May be raised on any thread.</summary>
    event Action? Changed;

    /// <exception cref="IOException">The location cannot be listed.</exception>
    IReadOnlyList<SourceFile> ReadFiles();
}
