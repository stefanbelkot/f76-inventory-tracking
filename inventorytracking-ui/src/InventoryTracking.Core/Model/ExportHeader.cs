namespace InventoryTracking.Core.Model;

/// <summary>The plain-text header of an export file. <paramref name="Currencies"/> is only present in player exports.</summary>
public sealed record ExportHeader(
    int Version,
    string Account,
    string Character,
    string Storage,
    string Snapshot,
    int Part,
    int? Parts,
    int? Rows,
    string? Title,
    IReadOnlyList<Currency>? Currencies = null);
