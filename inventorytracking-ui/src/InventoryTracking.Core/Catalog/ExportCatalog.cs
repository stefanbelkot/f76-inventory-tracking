using InventoryTracking.Core.Model;

namespace InventoryTracking.Core.Catalog;

/// <summary>An immutable snapshot of everything decoded from the source: groups, all items and load problems.</summary>
public sealed class ExportCatalog
{
    private readonly Dictionary<string, ItemGroup> _groupsById;
    private readonly Dictionary<string, Item> _itemsByKey;

    public ExportCatalog(
        long version,
        IReadOnlyList<ItemGroup> groups,
        IReadOnlyList<LoadIssue> issues,
        int fileCount,
        DateTime loadedAtUtc)
    {
        Version = version;
        Groups = groups;
        Issues = issues;
        FileCount = fileCount;
        LoadedAtUtc = loadedAtUtc;
        Items = groups.SelectMany(group => group.Items).ToList();
        _groupsById = groups.ToDictionary(group => group.Id);
        _itemsByKey = Items.ToDictionary(item => item.Key);
    }

    public static ExportCatalog Empty { get; } = new(0, [], [], 0, DateTime.UtcNow);

    /// <summary>Increases whenever the catalog is rebuilt; the page polls it to know when to refresh.</summary>
    public long Version { get; }

    public IReadOnlyList<ItemGroup> Groups { get; }

    public IReadOnlyList<Item> Items { get; }

    public IReadOnlyList<LoadIssue> Issues { get; }

    /// <summary>Number of export files found in the source, damaged ones included.</summary>
    public int FileCount { get; }

    public DateTime LoadedAtUtc { get; }

    public ItemGroup? FindGroup(string id) => _groupsById.GetValueOrDefault(id);

    public Item? FindItem(string key) => _itemsByKey.GetValueOrDefault(key);
}
