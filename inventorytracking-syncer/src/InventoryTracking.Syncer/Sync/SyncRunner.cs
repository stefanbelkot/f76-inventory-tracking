using InventoryTracking.Core.Sources;
using InventoryTracking.Syncer.Settings;

namespace InventoryTracking.Syncer.Sync;

/// <summary>
/// Runs <see cref="SyncEngine"/> rounds: at start, after file changes (debounced), on a periodic rescan and with back-off after
/// trouble. File watchers miss events on some folders, so the rescan is what guarantees convergence.
/// </summary>
public sealed class SyncRunner : IDisposable
{
    private static readonly TimeSpan[] Backoff = [TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(10)];

    private readonly SettingsStore _store;
    private readonly SyncEngine _engine;
    private readonly ILog _log;
    private readonly TimeSpan _debounce;
    private readonly SemaphoreSlim _trigger = new(0);
    private readonly List<FileSystemWatcher> _watchers = [];
    private CancellationTokenSource? _cancellation;
    private Task? _loop;
    private int _failures;

    public SyncRunner(SettingsStore store, SyncEngine engine, ILog log, TimeSpan? debounce = null)
    {
        _store = store;
        _engine = engine;
        _log = log;
        _debounce = debounce ?? TimeSpan.FromSeconds(2);
    }

    /// <summary>Raised on a thread-pool thread after every round and when a round starts.</summary>
    public event Action<SyncOutcome>? StatusChanged;

    public SyncOutcome Last { get; private set; } = new(SyncState.NotConfigured, "Not configured");

    public void Start()
    {
        if (_loop is not null)
        {
            return;
        }

        _cancellation = new CancellationTokenSource();
        Watch();
        _loop = Task.Run(() => LoopAsync(_cancellation.Token));
    }

    /// <summary>Settings changed (folders, session): watch the new folders and run a round soon.</summary>
    public void Reconfigure()
    {
        _failures = 0;
        Watch();
        Request();
    }

    public void Request() => _trigger.Release();

    private void Watch()
    {
        lock (_watchers)
        {
            foreach (var watcher in _watchers)
            {
                watcher.Dispose();
            }

            _watchers.Clear();
            foreach (var folder in _store.Read(s => s.Folders.Where(f => f.Enabled).Select(f => f.Path).ToList()))
            {
                try
                {
                    if (!Directory.Exists(folder))
                    {
                        continue;
                    }

                    var watcher = new FileSystemWatcher(folder, ExportFileName.SearchPattern)
                    {
                        NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
                        IncludeSubdirectories = false,
                        EnableRaisingEvents = true,
                    };
                    watcher.Changed += (_, _) => Request();
                    watcher.Created += (_, _) => Request();
                    watcher.Deleted += (_, _) => Request();
                    watcher.Renamed += (_, _) => Request();
                    _watchers.Add(watcher);
                }
                catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException)
                {
                    _log.Write($"cannot watch {folder}: {ex.Message}");
                }
            }
        }
    }

    private async Task LoopAsync(CancellationToken cancellation)
    {
        var wait = TimeSpan.Zero;
        while (!cancellation.IsCancellationRequested)
        {
            try
            {
                if (wait > TimeSpan.Zero)
                {
                    // A trigger ends the wait early; back-off waits are also cut short by user actions (Reconfigure/Sync now).
                    if (await _trigger.WaitAsync(wait, cancellation))
                    {
                        await Task.Delay(_debounce, cancellation);
                        while (_trigger.CurrentCount > 0)
                        {
                            await _trigger.WaitAsync(cancellation);
                        }
                    }
                }

                StatusChanged?.Invoke(Last = new SyncOutcome(SyncState.Syncing, "Syncing..."));
                var outcome = await _engine.RunOnceAsync(cancellation);
                Last = outcome;
                StatusChanged?.Invoke(outcome);
                _failures = outcome.Retry && outcome.State != SyncState.Ok ? _failures + 1 : 0;
                var rescan = TimeSpan.FromSeconds(Math.Clamp(_store.Read(s => s.RescanSeconds), 10, 3600));
                wait = outcome.State switch
                {
                    SyncState.SessionExpired or SyncState.NotConfigured => TimeSpan.FromHours(1),
                    _ when outcome.Retry && outcome.State != SyncState.Ok => Backoff[Math.Min(_failures, Backoff.Length) - 1],
                    _ when outcome.Retry => TimeSpan.FromSeconds(5),
                    _ => rescan,
                };
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _log.Write($"unexpected error: {ex}");
                Last = new SyncOutcome(SyncState.Problem, "Unexpected error, see the log", Retry: true);
                StatusChanged?.Invoke(Last);
                wait = TimeSpan.FromMinutes(1);
            }
        }
    }

    public void Dispose()
    {
        _cancellation?.Cancel();
        try
        {
            _loop?.Wait(TimeSpan.FromSeconds(2));
        }
        catch (AggregateException)
        {
            // Cancelled while waiting.
        }

        lock (_watchers)
        {
            foreach (var watcher in _watchers)
            {
                watcher.Dispose();
            }

            _watchers.Clear();
        }

        _cancellation?.Dispose();
    }
}
