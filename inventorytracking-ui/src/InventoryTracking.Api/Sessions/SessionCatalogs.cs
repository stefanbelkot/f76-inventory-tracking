using InventoryTracking.Core.Catalog;
using InventoryTracking.Core.Sources;
using InventoryTracking.Core.Types;

namespace InventoryTracking.Server.Sessions;

/// <summary>
/// The in-memory catalog of each session, built from its folder on first use. A catalog that was not used for the
/// memory-idle time is dropped by <see cref="EvictIdle"/> and is read from disk again on the next request. After an
/// upload or delete, <see cref="Refresh"/> rebuilds the whole catalog of that session.
/// </summary>
public sealed class SessionCatalogs : IDisposable
{
    private readonly SessionStore _store;
    private readonly ItemTypeTable _types;
    private readonly TimeProvider _time;
    private readonly Func<(int Generation, IReadOnlyList<string> Folders)>? _localFolders;
    private readonly object _gate = new();
    private readonly Dictionary<string, Entry> _entries = new(StringComparer.Ordinal);

    /// <param name="localFolders">In both mode: the local folders (with a number that changes when they do) that every session reads as well.</param>
    public SessionCatalogs(
        SessionStore store,
        ItemTypeTable types,
        TimeProvider? time = null,
        Func<(int Generation, IReadOnlyList<string> Folders)>? localFolders = null)
    {
        _store = store;
        _types = types;
        _time = time ?? TimeProvider.System;
        _localFolders = localFolders;
    }

    /// <summary>The number of catalogs currently held in memory.</summary>
    public int InMemory
    {
        get
        {
            lock (_gate)
            {
                return _entries.Count;
            }
        }
    }

    /// <summary>The catalog of an existing session, or null when the session does not exist (or has expired).</summary>
    public ExportCatalogService? Get(string? id)
    {
        if (!_store.Touch(id))
        {
            return null;
        }

        lock (_gate)
        {
            var generation = _localFolders?.Invoke().Generation ?? 0;
            if (_entries.TryGetValue(id!, out var entry) && entry.Generation == generation)
            {
                entry.LastUsed = Now;
                return entry.Catalog;
            }

            entry?.Catalog.Dispose();
            var created = new Entry(Build(id!), generation) { LastUsed = Now };
            _entries[id!] = created;
            return created.Catalog;
        }
    }

    /// <summary>Reads the whole session folder again (after an upload or a delete).</summary>
    public void Refresh(string id)
    {
        lock (_gate)
        {
            if (_entries.TryGetValue(id, out var entry))
            {
                entry.Catalog.Reload();
                entry.LastUsed = Now;
            }
        }
    }

    /// <summary>Drops the catalog of a deleted session.</summary>
    public void Forget(string id)
    {
        lock (_gate)
        {
            if (_entries.Remove(id, out var entry))
            {
                entry.Catalog.Dispose();
            }
        }
    }

    /// <summary>Drops every catalog that was not used for the memory-idle time. Returns how many were dropped.</summary>
    public int EvictIdle()
    {
        var limit = Now - _store.Options.MemoryIdle;
        lock (_gate)
        {
            var idle = _entries.Where(pair => pair.Value.LastUsed < limit).Select(pair => pair.Key).ToList();
            foreach (var id in idle)
            {
                _entries[id].Catalog.Dispose();
                _entries.Remove(id);
            }

            return idle.Count;
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            foreach (var entry in _entries.Values)
            {
                entry.Catalog.Dispose();
            }

            _entries.Clear();
        }
    }

    private DateTime Now => _time.GetUtcNow().UtcDateTime;

    private ExportCatalogService Build(string id)
    {
        // Sessions are reloaded explicitly after every change, so their folders are not watched.
        var sources = new List<IExportSource> { new LocalFolderSource(_store.FolderOf(id), watch: false) };
        if (_localFolders is not null)
        {
            sources.AddRange(_localFolders().Folders.Select(folder => new LocalFolderSource(folder)));
        }

        var catalog = new ExportCatalogService(_types);
        catalog.Attach(new CompositeExportSource(sources));
        return catalog;
    }

    private sealed class Entry(ExportCatalogService catalog, int generation)
    {
        public ExportCatalogService Catalog { get; } = catalog;

        public int Generation { get; } = generation;

        public DateTime LastUsed { get; set; }
    }
}
