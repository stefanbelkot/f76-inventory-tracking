using InventoryTracking.Core.Model;
using InventoryTracking.Core.Sources;
using InventoryTracking.Core.Types;

namespace InventoryTracking.Core.Catalog;

/// <summary>
/// Owns the current in-memory catalog. It rebuilds the catalog from the attached source when asked
/// or when the source reports a change, and bumps the data version each time.
/// </summary>
public sealed class ExportCatalogService(ItemTypeTable types) : IDisposable
{
    private readonly object _gate = new();
    private IExportSource? _source;
    private volatile ExportCatalog _current = ExportCatalog.Empty;

    public ExportCatalog Current => _current;

    /// <summary>Replaces the source (null detaches it) and loads it immediately.</summary>
    public void Attach(IExportSource? source)
    {
        lock (_gate)
        {
            if (_source is not null)
            {
                _source.Changed -= Reload;
                _source.Dispose();
            }

            _source = source;
            if (_source is not null)
            {
                _source.Changed += Reload;
            }

            ReloadLocked();
        }
    }

    public void Reload()
    {
        lock (_gate)
        {
            ReloadLocked();
        }
    }

    public void Dispose() => Attach(null);

    private void ReloadLocked()
    {
        var version = _current.Version + 1;
        if (_source is null)
        {
            _current = new ExportCatalog(version, [], [], 0, DateTime.UtcNow);
            return;
        }

        try
        {
            _current = CatalogBuilder.Build(_source.ReadFiles(), types, version);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            var issue = new LoadIssue("", IssueKind.Unreadable, ex.Message);
            _current = new ExportCatalog(version, [], [issue], 0, DateTime.UtcNow);
        }
    }
}
