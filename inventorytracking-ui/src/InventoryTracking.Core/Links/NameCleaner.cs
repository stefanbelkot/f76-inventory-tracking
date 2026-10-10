using System.Text.RegularExpressions;

namespace InventoryTracking.Core.Links;

/// <summary>
/// Turns a displayed item name into a search key: removes leading dots, the <c>¢</c> symbol,
/// star glyphs and a trailing count such as <c>(8)</c>.
/// </summary>
public static partial class NameCleaner
{
    public static string Clean(string name)
    {
        var text = DecorativeSymbols().Replace(name, " ");
        text = TrailingCount().Replace(text, "");
        text = text.Trim().TrimStart('.');
        return Whitespace().Replace(text, " ").Trim();
    }

    [GeneratedRegex("[¢★☆✦✧✩✪✫✬✭✮✯✰⭐]")]
    private static partial Regex DecorativeSymbols();

    [GeneratedRegex(@"\s*\(\d+\)\s*$")]
    private static partial Regex TrailingCount();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
