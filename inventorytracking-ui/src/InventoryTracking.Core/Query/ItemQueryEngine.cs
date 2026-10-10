using InventoryTracking.Core.Catalog;
using InventoryTracking.Core.Model;

namespace InventoryTracking.Core.Query;

/// <summary>Filters, sorts and pages the items of a catalog.</summary>
public static class ItemQueryEngine
{
    /// <summary>All items matching the query, in sort order. Returns null when the query names an unknown group.</summary>
    public static IReadOnlyList<Item>? Select(ExportCatalog catalog, ItemQuery query)
    {
        IReadOnlyList<Item> source = catalog.Items;
        if (!string.IsNullOrEmpty(query.GroupId))
        {
            var group = catalog.FindGroup(query.GroupId);
            if (group is null)
            {
                return null;
            }

            source = group.Items;
        }
        else if (query.Character is not null)
        {
            source = catalog.Groups
                .Where(group => group.Character == query.Character && (query.Account is null || group.Account == query.Account))
                .SelectMany(group => group.Items)
                .ToList();
        }

        return ItemSorter.Apply(source.Where(ItemFilter.Create(query)), query.Sort).ToList();
    }

    /// <summary>One page of <see cref="Select"/>; null when the query names an unknown group.</summary>
    public static ItemQueryResult? Run(ExportCatalog catalog, ItemQuery query)
    {
        var selected = Select(catalog, query);
        if (selected is null)
        {
            return null;
        }

        var pageSize = Math.Clamp(query.PageSize, 1, ItemQuery.MaxPageSize);
        var page = Math.Max(query.Page, 1);
        var items = selected.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        return new ItemQueryResult(items, page, pageSize, selected.Count, ItemTotals.Of(selected));
    }
}
