using InventoryTracking.Core.Decoding;
using InventoryTracking.Core.Sources;

namespace InventoryTracking.Core.Catalog;

/// <summary>A successfully decoded file together with where it came from.</summary>
internal sealed record DecodedPart(SourceFile File, DecodedFile Decoded);
