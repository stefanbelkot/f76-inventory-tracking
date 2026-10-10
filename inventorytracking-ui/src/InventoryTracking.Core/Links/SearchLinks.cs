namespace InventoryTracking.Core.Links;

/// <summary>Search links for one set of search terms.</summary>
public sealed record SearchLinks(string Terms, string GoogleUrl, string WikiUrl);

/// <summary>A Google search for one legendary effect line.</summary>
public sealed record EffectLink(int Star, string Text, string GoogleUrl);

/// <summary>The default links of an item: by name, and one Google link per starred legendary effect.</summary>
public sealed record ItemLinks(SearchLinks Name, IReadOnlyList<EffectLink> Effects);
