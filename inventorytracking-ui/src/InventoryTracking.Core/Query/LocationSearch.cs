using InventoryTracking.Core.Catalog;
using InventoryTracking.Core.Model;

namespace InventoryTracking.Core.Query;

public sealed record LocationHit(string GroupId, string Account, string Character, string Storage, int Rows, long TotalCount);

public sealed record LocationSearchResult(string Query, int Rows, long TotalCount, IReadOnlyList<LocationHit> Locations);

/// <summary>One search across every storage: where does an item (or effect) occur, and how many.</summary>
public static class LocationSearch
{
    public static LocationSearchResult Run(ExportCatalog catalog, string? query)
    {
        var text = new TextMatcher(query);
        var locations = catalog.Groups
            .Select(group => (Group: group, Hits: text.IsEmpty ? [] : group.Items.Where(item => text.Matches(item.SearchText)).ToList()))
            .Where(entry => entry.Hits.Count > 0)
            .Select(entry => new LocationHit(
                entry.Group.Id,
                entry.Group.Account,
                entry.Group.Character,
                entry.Group.Storage,
                entry.Hits.Count,
                entry.Hits.Sum(item => item.Count)))
            .ToList();
        return new LocationSearchResult(query ?? "", locations.Sum(hit => hit.Rows), locations.Sum(hit => hit.TotalCount), locations);
    }
}
