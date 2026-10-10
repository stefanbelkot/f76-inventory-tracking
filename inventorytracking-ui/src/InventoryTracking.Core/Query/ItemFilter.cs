using InventoryTracking.Core.Model;

namespace InventoryTracking.Core.Query;

/// <summary>Turns the filter part of an <see cref="ItemQuery"/> into a predicate.</summary>
internal static class ItemFilter
{
    public static Func<Item, bool> Create(ItemQuery query)
    {
        var text = new TextMatcher(query.Text);
        return item =>
            (query.Types.Count == 0 || item.Types.Any(query.Types.Contains))
            && (text.IsEmpty || text.Matches(item.SearchText))
            && (query.Legendary is null || item.Legendary == query.Legendary)
            && (query.Stars.Count == 0 || query.Stars.Contains(item.Stars))
            && (query.Favorite is null || item.Favorite == query.Favorite)
            && (query.Equipped is null || item.Equipped == query.Equipped)
            && (query.Quest is null || item.Quest == query.Quest)
            && (query.Vendor is null || item.Vendor == query.Vendor);
    }
}
