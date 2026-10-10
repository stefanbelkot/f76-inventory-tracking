using InventoryTracking.Core.Catalog;
using InventoryTracking.Core.Model;
using InventoryTracking.Core.Setup;
using InventoryTracking.Core.Types;
using InventoryTracking.Server.Settings;

namespace InventoryTracking.Server.Api.Contracts;

public sealed record ErrorResponse(string Error);

public sealed record CapabilitiesResponse(
    string Mode,
    bool FolderSetup,
    bool FolderBrowsing,
    bool FolderWatching,
    bool Shutdown,
    string Version,
    IReadOnlyList<ItemTypeDefinition> ItemTypes,
    bool Import,
    ImportLimits? ImportLimits);

/// <summary>What an upload session accepts and how long its data is kept.</summary>
public sealed record ImportLimits(long MaxFileBytes, int MaxFiles, long MaxSessionBytes, int SessionDays, int MemoryMinutes);

public sealed record SessionFileDto(string Name, long Size, DateTime ModifiedUtc);

public sealed record SessionResponse(string Id, IReadOnlyList<SessionFileDto> Files);

public sealed record UploadResultDto(string Name, bool Accepted, string? Error);

public sealed record UploadResponse(IReadOnlyList<UploadResultDto> Results, IReadOnlyList<SessionFileDto> Files);

public sealed record DeleteFilesRequest(IReadOnlyList<string>? Names);

public sealed record DeleteFilesResponse(int Deleted, IReadOnlyList<SessionFileDto> Files);

/// <summary>One configured export folder; <paramref name="Exists"/> is false when it is gone (for example a disconnected drive).</summary>
public sealed record FolderStatus(string Path, bool Exists, int ExportFiles);

public sealed record StatusResponse(
    long DataVersion,
    IReadOnlyList<FolderStatus> Folders,
    FolderOrigin? FolderOrigin,
    bool NeedsSetup,
    int FileCount,
    DateTime LoadedAtUtc,
    IReadOnlyList<LoadIssue> Issues);

public sealed record CandidatesResponse(IReadOnlyList<GameCandidate> Candidates);

public sealed record FolderRequest(string? Path, bool Confirm);

public sealed record FolderRemoveRequest(string? Path);

/// <param name="Saved">True when the folder was added to the saved list by this call.</param>
public sealed record FolderResponse(string Path, FolderKind Kind, int ExportFiles, bool Saved);

public sealed record GroupsResponse(long DataVersion, IReadOnlyList<CharacterEntry> Characters);

public sealed record ItemsResponse(
    IReadOnlyList<ItemSummary> Items,
    int Page,
    int PageSize,
    int TotalRows,
    ItemTotals Totals);

public sealed record PreferencesDto(string Theme, IReadOnlyList<string>? Columns);

public sealed record ShutdownResponse(bool Stopping);
