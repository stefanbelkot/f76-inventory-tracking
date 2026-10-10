using InventoryTracking.Core.Catalog;
using InventoryTracking.Core.Model;

namespace InventoryTracking.Core.Modules;

/// <summary>A legendary mod that is not junk. <paramref name="Stars"/> null means the name counts at every star tier.</summary>
public sealed record ValuableMod(string Name, int? Stars);

/// <param name="GroupId">One storage, or null for every storage.</param>
/// <param name="Valuable">The mods that are NOT junk; every other legendary mod counts as junk.</param>
/// <param name="LeadersPerModule">How many leaders one legendary module is worth (0.3 in trade chat).</param>
public sealed record ModuleSettings(string? GroupId, IReadOnlyList<ValuableMod> Valuable, decimal LeadersPerModule);

/// <summary>One mod name at one star tier, vendor and loose stacks combined. A valuable row is listed but not counted in any total.</summary>
public sealed record ModuleRow(string Name, int Stars, long Count, long ModulesEach, long Modules, decimal Leaders, bool Valuable);

/// <summary>A star tier: the sums cover the junk rows only; <paramref name="ValuableCount"/> is the number of valuable mods listed besides.</summary>
public sealed record StarTier(int Stars, long Count, long ModulesEach, long Modules, decimal Leaders, long ValuableCount, IReadOnlyList<ModuleRow> Rows);

public sealed record ModuleSummary(
    IReadOnlyList<StarTier> Tiers,
    long Count,
    long Modules,
    decimal Leaders,
    long ValuableCount);

/// <summary>
/// Turns legendary mods into legendary modules: a mod of n stars is worth 3 x 2^(n-1) modules (1 star 3, 2 stars 6, 3 stars 12, 4 stars 24).
/// Valuable mods are listed (flagged) but not counted.
/// </summary>
public static class ModuleCalculator
{
    public const int MaxStars = 20;
    public const string ModsType = "mods";

    public static long ModulesEach(int stars) => stars is < 1 or > MaxStars ? 0 : 3L << (stars - 1);

    /// <summary>Case, punctuation and a trailing plural s do not matter: "Pin-Pointer's", "Pinpointers" and "pinpointer" are one mod.</summary>
    public static string NameKey(string name)
    {
        var key = new string(name.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
        return key.Length > 3 && key.EndsWith('s') ? key[..^1] : key;
    }

    /// <summary>Returns null when the settings name an unknown group.</summary>
    public static ModuleSummary? Calculate(ExportCatalog catalog, ModuleSettings settings)
    {
        IReadOnlyList<Item> source = catalog.Items;
        if (!string.IsNullOrEmpty(settings.GroupId))
        {
            if (catalog.FindGroup(settings.GroupId) is not { } group)
            {
                return null;
            }

            source = group.Items;
        }

        var anyStar = settings.Valuable.Where(v => v.Stars is null).Select(v => NameKey(v.Name)).ToHashSet();
        var perStar = settings.Valuable.Where(v => v.Stars is not null).Select(v => (NameKey(v.Name), v.Stars!.Value)).ToHashSet();

        var junk = new Dictionary<(string Key, int Stars), (string Name, long Count)>();
        var valuable = new Dictionary<(string Key, int Stars), (string Name, long Count)>();
        foreach (var item in source)
        {
            if (item.Stars is < 1 or > MaxStars || !item.Types.Contains(ModsType))
            {
                continue;
            }

            var key = NameKey(item.Name);
            var target = anyStar.Contains(key) || perStar.Contains((key, item.Stars)) ? valuable : junk;
            var slot = (key, item.Stars);
            target[slot] = target.TryGetValue(slot, out var existing)
                ? (existing.Name, existing.Count + item.Count)
                : (item.Name, item.Count);
        }

        ModuleRow Row(KeyValuePair<(string Key, int Stars), (string Name, long Count)> entry, bool isValuable)
        {
            var each = ModulesEach(entry.Key.Stars);
            var modules = each * entry.Value.Count;
            return new ModuleRow(entry.Value.Name, entry.Key.Stars, entry.Value.Count, each, modules,
                modules * settings.LeadersPerModule, isValuable);
        }

        var tiers = junk.Select(entry => Row(entry, false)).Concat(valuable.Select(entry => Row(entry, true)))
            .GroupBy(row => row.Stars).OrderBy(g => g.Key)
            .Select(g =>
            {
                var rows = g.OrderBy(row => row.Name, StringComparer.OrdinalIgnoreCase).ToList();
                var counted = rows.Where(row => !row.Valuable).ToList();
                return new StarTier(g.Key, counted.Sum(r => r.Count), ModulesEach(g.Key), counted.Sum(r => r.Modules),
                    counted.Sum(r => r.Leaders), rows.Where(r => r.Valuable).Sum(r => r.Count), rows);
            }).ToList();
        return new ModuleSummary(tiers, tiers.Sum(t => t.Count), tiers.Sum(t => t.Modules), tiers.Sum(t => t.Leaders), tiers.Sum(t => t.ValuableCount));    }
}
