using InventoryTracking.Core.Model;

namespace InventoryTracking.Core.Query;

/// <summary>One page of the filtered list plus the totals of the whole filtered list.</summary>
public sealed record ItemQueryResult(IReadOnlyList<Item> Items, int Page, int PageSize, int TotalRows, ItemTotals Totals);
