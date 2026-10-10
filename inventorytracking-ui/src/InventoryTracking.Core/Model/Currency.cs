namespace InventoryTracking.Core.Model;

/// <summary>One currency balance of a character, as exported with the player inventory.</summary>
/// <param name="Type">The game's currency type number (0 caps, 1 legendary tokens, ...).</param>
/// <param name="Name">A readable name; "Currency n" for a type this viewer does not know.</param>
/// <param name="Amount">The balance.</param>
/// <param name="Max">The most the character can hold, when the game reports a limit.</param>
public sealed record Currency(int Type, string Name, long Amount, long? Max);

/// <summary>Names and display order of the currency types (from the game's own HUD scripts).</summary>
public static class CurrencyTable
{
    private static readonly (int Type, string Name)[] Known =
    [
        (0, "Caps"),
        (4, "Gold Bullion"),
        (1, "Legendary Tokens"),
        (11, "Expedition Stamps"),
        (10, "Ultracite Batteries"),
        (3, "Tadpole Badges"),
        (2, "Possum Badges"),
        (12, "Supplies"),
        (5, "Perk Coins"),
        (6, "Legendary Cores (tier 1)"),
        (7, "Legendary Cores (tier 2)"),
        (8, "Legendary Cores (tier 3)"),
        (9, "Legendary Cores (tier 4)"),
    ];

    public static string NameOf(int type) =>
        Known.FirstOrDefault(entry => entry.Type == type).Name ?? $"Currency {type}";

    /// <summary>The known currencies first in a fixed order, then unknown types by number.</summary>
    public static int OrderOf(int type)
    {
        var index = Array.FindIndex(Known, entry => entry.Type == type);
        return index >= 0 ? index : Known.Length + type;
    }
}
