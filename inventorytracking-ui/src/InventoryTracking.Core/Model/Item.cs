using System.Text.Json.Serialization;

namespace InventoryTracking.Core.Model;

/// <summary>
/// One inventory row. The first block mirrors the fields of the export file (final-spec 3.2);
/// the second block is derived by the viewer and is always overwritten after decoding.
/// </summary>
public sealed record Item
{
    public string Name { get; init; } = "";
    public long Count { get; init; }
    public long Id { get; init; }
    public long Value { get; init; }
    public int EquipState { get; init; }
    public long Filter { get; init; }
    public int Level { get; init; }
    public double Health { get; init; }
    public double MaxHealth { get; init; }
    public double Weight { get; init; }
    public bool Legendary { get; init; }
    public int Stars { get; init; }
    public bool Favorite { get; init; }
    public bool Quest { get; init; }
    public int Category { get; init; }
    public string? Card { get; init; }
    public int? LegendaryMods { get; init; }
    public string? Desc { get; init; }

    /// <summary>Set when the item is assigned to a CAMP machine (see <see cref="Model.Vending"/>); absent otherwise.</summary>
    public Vending? Vending { get; init; }

    public string Key { get; init; } = "";
    public string GroupId { get; init; } = "";
    public string Character { get; init; } = "";
    public string Storage { get; init; } = "";
    public IReadOnlyList<string> Types { get; init; } = [];
    public IReadOnlyList<Effect> Effects { get; init; } = [];

    /// <summary>Lower-case name and effect text used by the text search.</summary>
    [JsonIgnore]
    public string SearchText { get; init; } = "";

    public bool Equipped => EquipState != 0;

    /// <summary>True for an item that is currently listed at the player's vending machine.</summary>
    public bool Vendor => Vending is { Machine: Vending.VendingMachine };

    /// <summary>The listed price per item for a vendor item (null when not listed or when the game gave none).</summary>
    public long? Price => Vendor ? Vending!.Price : null;

    public double TotalWeight => Count * Weight;

    public long TotalValue => Count * Value;
}
