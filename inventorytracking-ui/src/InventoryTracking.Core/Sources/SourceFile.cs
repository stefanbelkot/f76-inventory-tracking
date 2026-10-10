namespace InventoryTracking.Core.Sources;

/// <summary>
/// One export file as delivered by a source: its bytes, or the reason it could not be read.
/// <paramref name="Origin"/> names the place it came from (a folder path) when several sources are combined.
/// </summary>
public sealed record SourceFile(string Name, DateTime LastWriteUtc, byte[]? Content, string? Error, string? Origin = null);
