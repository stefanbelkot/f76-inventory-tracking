namespace InventoryTracking.Core.Model;

/// <summary>One line of a legendary card description. <paramref name="Star"/> is null for special and unmarked lines.</summary>
public sealed record Effect(int? Star, string Text, string? Marker);
