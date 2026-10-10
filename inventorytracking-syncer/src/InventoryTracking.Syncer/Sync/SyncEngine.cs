using InventoryTracking.Syncer.Settings;

namespace InventoryTracking.Syncer.Sync;

/// <summary>
/// One synchronisation round: scan the enabled folders, upload what changed, remove from the session what this tool
/// uploaded earlier and is now gone. Rounds must not overlap; <see cref="SyncRunner"/> serialises them.
/// </summary>
public sealed class SyncEngine(SettingsStore store, Func<string, ISessionClient> clients, IFolderScanner scanner, ILog log, SyncLimits? limits = null)
{
    private readonly SyncLimits _limits = limits ?? new SyncLimits();
    private readonly HashSet<string> _rejected = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ISessionClient> _cache = new(StringComparer.OrdinalIgnoreCase);

    public async Task<SyncOutcome> RunOnceAsync(CancellationToken cancellation)
    {
        var (server, session, folders) = store.Read(s => (s.Server, s.Session, s.Folders.Where(f => f.Enabled).Select(f => f.Path).ToList()));
        if (!SessionLink.IsSessionId(session))
        {
            return new SyncOutcome(SyncState.NotConfigured, "No session set");
        }

        if (folders.Count == 0)
        {
            return new SyncOutcome(SyncState.NotConfigured, "No folder enabled");
        }

        try
        {
            var scan = scanner.Scan(folders);
            var known = store.Read(s => new Dictionary<string, string>(s.UploadedFor(session), StringComparer.OrdinalIgnoreCase));
            var plan = SyncPlanner.Plan(scan, known, _rejected, _limits);
            foreach (var skipped in plan.Skipped)
            {
                log.Write($"skipped {skipped}");
            }

            var uploaded = 0;
            var problems = new List<string>();
            if (plan.Uploads.Count > 0)
            {
                var results = await Client(server).UploadAsync(session, plan.Uploads, cancellation);
                var byName = plan.Uploads.ToDictionary(file => file.Name, StringComparer.OrdinalIgnoreCase);
                store.Update(settings =>
                {
                    var map = settings.UploadedFor(session);
                    foreach (var result in results)
                    {
                        if (!byName.TryGetValue(result.Name, out var file))
                        {
                            continue;
                        }

                        if (result.Accepted)
                        {
                            map[file.Name] = file.Hash;
                            uploaded++;
                            log.Write($"uploaded {file.Name}");
                        }
                        else
                        {
                            _rejected.Add($"{file.Name}|{file.Hash}");
                            problems.Add($"{file.Name}: {result.Error ?? "refused"}");
                            log.Write($"refused {file.Name}: {result.Error}");
                        }
                    }
                });
            }

            var deleted = 0;
            if (plan.Deletes.Count > 0)
            {
                await Client(server).DeleteAsync(session, plan.Deletes, cancellation);
                store.Update(settings =>
                {
                    var map = settings.UploadedFor(session);
                    foreach (var name in plan.Deletes)
                    {
                        map.Remove(name);
                    }
                });
                deleted = plan.Deletes.Count;
                log.Write($"removed from session: {string.Join(", ", plan.Deletes)}");
            }

            return Summarise(scan, plan, uploaded, deleted, problems);
        }
        catch (SessionException ex)
        {
            log.Write($"session error ({ex.Problem}): {ex.Message}");
            return ex.Problem switch
            {
                SessionProblem.Unknown => new SyncOutcome(SyncState.SessionExpired, "Session unknown or expired - update the session"),
                SessionProblem.Network => new SyncOutcome(SyncState.Offline, "Offline, will retry", Retry: true),
                _ => new SyncOutcome(SyncState.Problem, ex.Message, Retry: true),
            };
        }
    }

    /// <summary>Checks that a session exists on the server.</summary>
    public Task CheckAsync(string server, string session, CancellationToken cancellation) => Client(server).CheckSessionAsync(session, cancellation);

    public Task<string> CreateSessionAsync(string server, CancellationToken cancellation) => Client(server).CreateSessionAsync(cancellation);

    /// <summary>Forget refusals (for example after the session changed).</summary>
    public void ResetRejected() => _rejected.Clear();

    private static SyncOutcome Summarise(ScanResult scan, SyncPlan plan, int uploaded, int deleted, List<string> problems)
    {
        if (problems.Count > 0)
        {
            return new SyncOutcome(SyncState.Problem, problems[0], uploaded, deleted);
        }

        if (scan.Locked.Count > 0)
        {
            return new SyncOutcome(SyncState.Ok, $"{scan.Locked.Count} file(s) busy, retrying", uploaded, deleted, Retry: true);
        }

        if (!scan.AllFoldersAvailable && scan.Files.Count == 0)
        {
            return new SyncOutcome(SyncState.Ok, "Waiting for export files (folder not found yet)", uploaded, deleted);
        }

        var text = uploaded > 0 || deleted > 0
            ? $"Synced {DateTime.Now:HH:mm}: {uploaded} uploaded, {deleted} removed"
            : $"Up to date ({scan.Files.Count} files)";
        return new SyncOutcome(SyncState.Ok, plan.Skipped.Count > 0 ? $"{text}; {plan.Skipped.Count} skipped" : text, uploaded, deleted);
    }

    private ISessionClient Client(string server)
    {
        if (!_cache.TryGetValue(server, out var client))
        {
            client = clients(server);
            _cache[server] = client;
        }

        return client;
    }
}
