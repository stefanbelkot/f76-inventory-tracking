using System.Text.Json;

namespace InventoryTracking.Core.Types;

/// <summary>Maps the <c>filter</c> bit mask of an item to item types using a data-driven table.</summary>
public sealed class ItemTypeTable
{
    public const string OtherId = "other";

    private const string ResourceName = "InventoryTracking.Core.Types.itemtypes.json";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly long _knownMask;

    public ItemTypeTable(IEnumerable<ItemTypeDefinition> definitions)
    {
        var list = definitions.ToList();
        _knownMask = list.SelectMany(type => type.Bits).Aggregate(0L, (mask, bit) => mask | bit);
        Types = [.. list, new ItemTypeDefinition(OtherId, "Other", [])];
    }

    /// <summary>The table loaded from the embedded <c>itemtypes.json</c>.</summary>
    public static ItemTypeTable Default { get; } = LoadEmbedded();

    /// <summary>All types in display order, ending with the catch-all "other" type.</summary>
    public IReadOnlyList<ItemTypeDefinition> Types { get; }

    /// <summary>Returns the ids of every type the mask belongs to; "other" is added for unknown bits.</summary>
    public IReadOnlyList<string> Classify(long filter)
    {
        var ids = Types
            .Where(type => type.Bits.Any(bit => (filter & bit) != 0))
            .Select(type => type.Id)
            .ToList();
        if ((filter & ~_knownMask) != 0 || filter == 0)
        {
            ids.Add(OtherId);
        }

        return ids;
    }

    private static ItemTypeTable LoadEmbedded()
    {
        using var stream = typeof(ItemTypeTable).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Embedded resource {ResourceName} is missing.");
        var definitions = JsonSerializer.Deserialize<List<ItemTypeDefinition>>(stream, JsonOptions)
            ?? throw new InvalidOperationException("The item type table is empty.");
        return new ItemTypeTable(definitions);
    }
}
