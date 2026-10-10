using InventoryTracking.Core.Model;

namespace InventoryTracking.Core.Query;

/// <summary>Applies an <see cref="ItemSort"/>. The sort is stable, so equal rows keep their file order.</summary>
internal static class ItemSorter
{
    public static IEnumerable<Item> Apply(IEnumerable<Item> items, ItemSort? sort)
    {
        if (sort is null)
        {
            return items;
        }

        return sort.Field switch
        {
            SortField.Name => Order(items, sort, item => item.Name, StringComparer.OrdinalIgnoreCase),
            SortField.Character => Order(items, sort, item => item.Character, StringComparer.OrdinalIgnoreCase),
            SortField.Storage => Order(items, sort, item => item.Storage, StringComparer.OrdinalIgnoreCase),
            SortField.Type => Order(items, sort, item => item.Types.FirstOrDefault() ?? "", StringComparer.Ordinal),
            SortField.Count => Order(items, sort, item => item.Count),
            SortField.Level => Order(items, sort, item => item.Level),
            SortField.Stars => Order(items, sort, item => item.Stars),
            SortField.Weight => Order(items, sort, item => item.Weight),
            SortField.TotalWeight => Order(items, sort, item => item.TotalWeight),
            SortField.Value => Order(items, sort, item => item.Value),
            SortField.TotalValue => Order(items, sort, item => item.TotalValue),
            // Items without a listed price sort as the cheapest, so descending shows the dearest listing first.
            SortField.Price => Order(items, sort, item => item.Price ?? -1),
            SortField.Favorite => Order(items, sort, item => item.Favorite),
            SortField.Equipped => Order(items, sort, item => item.Equipped),
            _ => items,
        };
    }

    private static IEnumerable<Item> Order<TKey>(IEnumerable<Item> items, ItemSort sort, Func<Item, TKey> key, IComparer<TKey>? comparer = null) =>
        sort.Descending ? items.OrderByDescending(key, comparer) : items.OrderBy(key, comparer);
}
