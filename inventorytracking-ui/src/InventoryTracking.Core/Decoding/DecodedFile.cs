using InventoryTracking.Core.Model;

namespace InventoryTracking.Core.Decoding;

/// <summary>The header and rows of one export file.</summary>
public sealed record DecodedFile(ExportHeader Header, IReadOnlyList<Item> Items);
