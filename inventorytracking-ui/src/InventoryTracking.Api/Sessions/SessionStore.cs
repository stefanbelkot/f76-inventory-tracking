using System.Globalization;
using System.Text.RegularExpressions;
using InventoryTracking.Core.Decoding;
using InventoryTracking.Core.Sources;
using InventoryTracking.Server.Hosting;

namespace InventoryTracking.Server.Sessions;

/// <summary>One uploaded file of a session.</summary>
public sealed record SessionFile(string Name, long Size, DateTime ModifiedUtc);

/// <summary>The outcome of storing one uploaded file; <paramref name="Error"/> says why it was refused.</summary>
public sealed record UploadResult(string Name, bool Accepted, string? Error);

public sealed class SessionLimitException(string message) : Exception(message);

/// <summary>
/// The session folders on disk: <c>&lt;root&gt;/&lt;id&gt;/itemdump_*.json</c>. A session id is a random 128-bit GUID without
/// dashes; whoever knows it may read, upload and delete. Every use refreshes the session's "last used" time, and a
/// session that was not used for the configured lifetime is deleted by <see cref="DeleteExpired"/>.
/// </summary>
public sealed partial class SessionStore
{
    private const string UsedMarker = ".used";
    private static readonly TimeSpan TouchInterval = TimeSpan.FromMinutes(1);

    private readonly WebOptions _options;
    private readonly TimeProvider _time;
    private readonly object _gate = new();
    private readonly Dictionary<string, DateTime> _touched = new(StringComparer.Ordinal);

    public SessionStore(string root, WebOptions options, TimeProvider? time = null)
    {
        Root = Path.GetFullPath(root);
        _options = options;
        _time = time ?? TimeProvider.System;
    }

    public string Root { get; }

    public WebOptions Options => _options;

    /// <summary>True for a well-formed id: exactly 32 lowercase hex digits (so it can never escape the root folder).</summary>
    public static bool IsValidId(string? id) => id is { Length: 32 } && SessionId().IsMatch(id);

    public string FolderOf(string id) => IsValidId(id) ? Path.Combine(Root, id) : throw new ArgumentException("Not a session id.", nameof(id));

    /// <exception cref="SessionLimitException">The server already holds the most sessions it allows.</exception>
    public string Create()
    {
        lock (_gate)
        {
            Directory.CreateDirectory(Root);
            if (Directory.EnumerateDirectories(Root).Count() >= _options.MaxSessions)
            {
                throw new SessionLimitException("The server holds the most sessions it allows. Try again later.");
            }

            var id = Guid.NewGuid().ToString("N");
            Directory.CreateDirectory(FolderOf(id));
            Mark(id);
            return id;
        }
    }

    /// <summary>True when the session exists; the use is recorded (at most once a minute) so it does not expire while it is in use.</summary>
    public bool Touch(string? id)
    {
        if (!IsValidId(id) || !Directory.Exists(FolderOf(id!)))
        {
            return false;
        }

        var now = _time.GetUtcNow().UtcDateTime;
        lock (_gate)
        {
            if (!_touched.TryGetValue(id!, out var last) || now - last >= TouchInterval)
            {
                _touched[id!] = now;
                Mark(id!);
            }
        }

        return true;
    }

    public IReadOnlyList<SessionFile> List(string id)
    {
        var folder = FolderOf(id);
        if (!Directory.Exists(folder))
        {
            return [];
        }

        return Directory.EnumerateFiles(folder, ExportFileName.SearchPattern)
            .Where(path => ExportFileName.IsExportFile(Path.GetFileName(path)))
            .Select(path => new FileInfo(path))
            .Select(info => new SessionFile(info.Name, info.Length, info.LastWriteTimeUtc))
            .OrderBy(file => file.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>Checks and stores one file; a file with the same name replaces the earlier one.</summary>
    public UploadResult Save(string id, string fileName, byte[] content)
    {
        var name = Path.GetFileName(fileName ?? "");
        if (!IsAcceptableName(name))
        {
            return new UploadResult(name, false, "Only export files named itemdump_<account>_<character>_<storage>_<part>.json are accepted.");
        }

        if (content.Length == 0 || content.Length > _options.MaxFileBytes)
        {
            return new UploadResult(name, false, content.Length == 0 ? "The file is empty." : $"The file is larger than {_options.MaxFileBytes / 1024} KB.");
        }

        try
        {
            ExportFileDecoder.Decode(content);
        }
        catch (ExportDecodeException ex)
        {
            return new UploadResult(name, false, ex.Message);
        }

        lock (_gate)
        {
            var folder = FolderOf(id);
            var existing = List(id);
            var replaced = existing.FirstOrDefault(file => file.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (replaced is null && existing.Count >= _options.MaxFilesPerSession)
            {
                return new UploadResult(name, false, $"A session holds at most {_options.MaxFilesPerSession} files.");
            }

            if (existing.Sum(file => file.Size) - (replaced?.Size ?? 0) + content.Length > _options.MaxSessionBytes)
            {
                return new UploadResult(name, false, $"A session holds at most {_options.MaxSessionBytes / 1024 / 1024} MB of files.");
            }

            var target = Path.Combine(folder, replaced?.Name ?? name);
            var temporary = Path.Combine(folder, $"{name}.{Guid.NewGuid():N}.tmp");
            File.WriteAllBytes(temporary, content);
            File.Move(temporary, target, overwrite: true);
            Mark(id);
            return new UploadResult(replaced?.Name ?? name, true, null);
        }
    }

    /// <summary>Deletes the named files of a session; names that are not export files or do not exist are ignored. Returns how many were deleted.</summary>
    public int Delete(string id, IEnumerable<string> names)
    {
        lock (_gate)
        {
            var folder = FolderOf(id);
            var deleted = 0;
            foreach (var name in names.Select(name => Path.GetFileName(name ?? "")).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var path = Path.Combine(folder, name);
                if (IsAcceptableName(name) && File.Exists(path))
                {
                    File.Delete(path);
                    deleted++;
                }
            }

            Mark(id);
            return deleted;
        }
    }

    public void DeleteSession(string id)
    {
        lock (_gate)
        {
            var folder = FolderOf(id);
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
            }

            _touched.Remove(id);
        }
    }

    /// <summary>Deletes every session folder that has not been used for the session lifetime. Returns how many were deleted.</summary>
    public int DeleteExpired()
    {
        if (!Directory.Exists(Root))
        {
            return 0;
        }

        var limit = _time.GetUtcNow().UtcDateTime - _options.SessionLifetime;
        var deleted = 0;
        lock (_gate)
        {
            foreach (var folder in Directory.EnumerateDirectories(Root))
            {
                var id = Path.GetFileName(folder);
                if (IsValidId(id) && LastUsed(folder) < limit)
                {
                    Directory.Delete(folder, recursive: true);
                    _touched.Remove(id);
                    deleted++;
                }
            }
        }

        return deleted;
    }

    private static bool IsAcceptableName(string name) =>
        name.Length is > 0 and <= 120 && ExportFileName.IsExportFile(name) && SafeName().IsMatch(name);

    /// <summary>Records "used now" as the modification time of a marker file (so the time survives a restart of the server).</summary>
    private void Mark(string id)
    {
        var now = _time.GetUtcNow().UtcDateTime;
        var marker = Path.Combine(FolderOf(id), UsedMarker);
        File.WriteAllText(marker, now.ToString("o", CultureInfo.InvariantCulture));
        File.SetLastWriteTimeUtc(marker, now);
    }

    private static DateTime LastUsed(string folder)
    {
        var marker = Path.Combine(folder, UsedMarker);
        return File.Exists(marker) ? File.GetLastWriteTimeUtc(marker) : Directory.GetLastWriteTimeUtc(folder);
    }

    [GeneratedRegex("^[0-9a-f]{32}$")]
    private static partial Regex SessionId();

    [GeneratedRegex(@"^itemdump_[A-Za-z0-9_.\-]+\.json$", RegexOptions.IgnoreCase)]
    private static partial Regex SafeName();
}
