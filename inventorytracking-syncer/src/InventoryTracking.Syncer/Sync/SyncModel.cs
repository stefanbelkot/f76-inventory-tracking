using System.Security.Cryptography;
using System.Text.RegularExpressions;
using InventoryTracking.Core.Sources;

namespace InventoryTracking.Syncer.Sync;

/// <summary>The server's upload limits (viewer options <c>MaxFileBytes</c>, <c>MaxFilesPerSession</c>, <c>MaxSessionBytes</c>).</summary>
public sealed record SyncLimits(long MaxFileBytes = 262_144, int MaxFiles = 100, long MaxSessionBytes = 5_242_880);

/// <summary>An export file read from disk: the bytes are kept so the hash and the upload see the same content.</summary>
public sealed record LocalFile(string Name, string FullPath, DateTime LastWriteUtc, byte[] Content, string Hash)
{
    public long Size => Content.Length;

    public static string HashOf(byte[] content) => Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
}

/// <param name="Present">Every export file name seen in the folders, readable or not (needed to tell "deleted" from "locked").</param>
/// <param name="AllFoldersAvailable">False when an enabled folder is missing or cannot be read: nothing may be deleted then.</param>
public sealed record ScanResult(
    IReadOnlyList<LocalFile> Files,
    IReadOnlySet<string> Present,
    IReadOnlyList<string> Locked,
    IReadOnlyList<string> Oversized,
    bool AllFoldersAvailable);

public interface IFolderScanner
{
    ScanResult Scan(IReadOnlyList<string> folders);
}

public sealed record UploadOutcome(string Name, bool Accepted, string? Error);

/// <summary>The three calls the Syncer needs from the viewer's web API.</summary>
public interface ISessionClient
{
    /// <summary>Creates a session; returns its id.</summary>
    Task<string> CreateSessionAsync(CancellationToken cancellation);

    Task CheckSessionAsync(string session, CancellationToken cancellation);

    Task<IReadOnlyList<UploadOutcome>> UploadAsync(string session, IReadOnlyList<LocalFile> files, CancellationToken cancellation);

    Task DeleteAsync(string session, IReadOnlyList<string> names, CancellationToken cancellation);
}

public enum SessionProblem
{
    /// <summary>404 or 401: the session does not exist (any more).</summary>
    Unknown,

    /// <summary>The server refused or failed (4xx other than 404, 5xx).</summary>
    Server,

    /// <summary>No connection, timeout, TLS or redirect trouble.</summary>
    Network,
}

public sealed class SessionException(SessionProblem problem, string message, Exception? inner = null) : Exception(message, inner)
{
    public SessionProblem Problem { get; } = problem;
}

public enum SyncState
{
    NotConfigured,
    Idle,
    Syncing,
    Ok,
    SessionExpired,
    Offline,
    Problem,
}

/// <param name="Retry">The caller should try again later (back-off) instead of waiting for the next change.</param>
public sealed record SyncOutcome(SyncState State, string Message, int Uploaded = 0, int Deleted = 0, bool Retry = false);

/// <summary>What one round has to do, decided from the scan and what was uploaded before.</summary>
public sealed record SyncPlan(IReadOnlyList<LocalFile> Uploads, IReadOnlyList<string> Deletes, IReadOnlyList<string> Skipped);

public static partial class SyncPlanner
{
    public static bool IsAcceptableName(string name) =>
        name.Length is > 0 and <= 120 && ExportFileName.IsExportFile(name) && SafeName().IsMatch(name);

    /// <param name="uploaded">Name to hash of what this tool uploaded to this session earlier.</param>
    /// <param name="rejected">"name|hash" entries the server already refused; they are not sent again until the content changes.</param>
    public static SyncPlan Plan(ScanResult scan, IReadOnlyDictionary<string, string> uploaded, IReadOnlySet<string> rejected, SyncLimits limits)
    {
        var skipped = new List<string>();
        var candidates = new List<LocalFile>();
        foreach (var file in scan.Files.OrderBy(file => file.Name, StringComparer.OrdinalIgnoreCase))
        {
            if (!IsAcceptableName(file.Name))
            {
                skipped.Add($"{file.Name}: not an export file name");
            }
            else if (file.Size == 0)
            {
                skipped.Add($"{file.Name}: empty");
            }
            else if (file.Size > limits.MaxFileBytes)
            {
                skipped.Add($"{file.Name}: larger than {limits.MaxFileBytes / 1024} KB");
            }
            else if ((!uploaded.TryGetValue(file.Name, out var known) || known != file.Hash) && !rejected.Contains($"{file.Name}|{file.Hash}"))
            {
                candidates.Add(file);
            }
        }

        skipped.AddRange(scan.Oversized.Select(name => $"{name}: larger than {limits.MaxFileBytes / 1024} KB"));

        // A session holds a limited number of files and bytes; whatever does not fit is reported, not sent.
        var uploads = new List<LocalFile>();
        var names = new HashSet<string>(uploaded.Keys, StringComparer.OrdinalIgnoreCase);
        var bytes = scan.Files.Where(file => uploaded.ContainsKey(file.Name)).Sum(file => file.Size);
        foreach (var file in candidates)
        {
            var isNew = names.Add(file.Name);
            if (isNew && names.Count > limits.MaxFiles)
            {
                names.Remove(file.Name);
                skipped.Add($"{file.Name}: a session holds at most {limits.MaxFiles} files");
                continue;
            }

            if (bytes + file.Size > limits.MaxSessionBytes)
            {
                skipped.Add($"{file.Name}: a session holds at most {limits.MaxSessionBytes / 1024 / 1024} MB");
                continue;
            }

            bytes += file.Size;
            uploads.Add(file);
        }

        var deletes = scan.AllFoldersAvailable
            ? uploaded.Keys.Where(name => !scan.Present.Contains(name)).Order(StringComparer.OrdinalIgnoreCase).ToList()
            : [];
        return new SyncPlan(uploads, deletes, skipped);
    }

    [GeneratedRegex(@"^itemdump_[A-Za-z0-9_.\-]+\.json$", RegexOptions.IgnoreCase)]
    private static partial Regex SafeName();
}
