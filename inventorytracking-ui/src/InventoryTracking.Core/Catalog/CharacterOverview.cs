using InventoryTracking.Core.Model;

namespace InventoryTracking.Core.Catalog;

public sealed record CharacterEntry(string Account, string Character, IReadOnlyList<ItemGroup> Storages);

/// <summary>Arranges the groups of a catalog by character, with the storages in game order.</summary>
public static class CharacterOverview
{
    private static readonly string[] StorageOrder = ["player", "stash", "scrap", "ammo", "aid"];

    public static IReadOnlyList<CharacterEntry> Build(ExportCatalog catalog) =>
        catalog.Groups
            .GroupBy(group => (group.Account, group.Character))
            .OrderBy(entry => entry.Key.Account, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.Key.Character, StringComparer.OrdinalIgnoreCase)
            .Select(entry => new CharacterEntry(
                entry.Key.Account,
                entry.Key.Character,
                entry.OrderBy(group => OrderOf(group.Storage)).ThenBy(group => group.Storage, StringComparer.Ordinal).ToList()))
            .ToList();

    private static int OrderOf(string storage)
    {
        var index = Array.IndexOf(StorageOrder, storage);
        return index < 0 ? StorageOrder.Length : index;
    }
}
