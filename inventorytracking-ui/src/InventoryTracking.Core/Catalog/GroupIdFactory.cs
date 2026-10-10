using System.Text.RegularExpressions;

namespace InventoryTracking.Core.Catalog;

/// <summary>Builds stable, URL-safe group ids from the group's identity.</summary>
internal sealed partial class GroupIdFactory
{
    private readonly HashSet<string> _used = new(StringComparer.Ordinal);

    public string Create(string account, string character, string storage, string snapshot)
    {
        var baseId = Unsafe().Replace($"{account}-{character}-{storage}-{snapshot}".ToLowerInvariant(), "_");
        var id = baseId;
        for (var suffix = 2; !_used.Add(id); suffix++)
        {
            id = $"{baseId}-{suffix}";
        }

        return id;
    }

    [GeneratedRegex("[^a-z0-9_-]")]
    private static partial Regex Unsafe();
}
