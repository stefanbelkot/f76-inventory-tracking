namespace InventoryTracking.Core.Types;

/// <summary>One item type and the bits of the item <c>filter</c> mask that select it.</summary>
public sealed record ItemTypeDefinition(string Id, string Name, IReadOnlyList<long> Bits);
