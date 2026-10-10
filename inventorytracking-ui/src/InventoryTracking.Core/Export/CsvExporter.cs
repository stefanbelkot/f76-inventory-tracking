using System.Globalization;
using System.Text;
using InventoryTracking.Core.Model;

namespace InventoryTracking.Core.Export;

/// <summary>Writes items as CSV (RFC 4180 quoting, UTF-8 with byte order mark so spreadsheets read the symbols correctly).</summary>
public static class CsvExporter
{
    private static readonly string[] Header =
    [
        "character", "storage", "name", "count", "types", "filter", "level", "stars", "weight", "totalWeight",
        "value", "totalValue", "legendary", "favorite", "equipped", "quest", "vendor", "price", "effects",
    ];

    public static byte[] Write(IEnumerable<Item> items)
    {
        var text = new StringBuilder();
        AppendRow(text, Header);
        foreach (var item in items)
        {
            AppendRow(text, Cells(item));
        }

        return [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(text.ToString())];
    }

    private static string[] Cells(Item item) =>
    [
        item.Character,
        item.Storage,
        item.Name,
        Number(item.Count),
        string.Join(", ", item.Types),
        Number(item.Filter),
        Number(item.Level),
        Number(item.Stars),
        Number(item.Weight),
        Number(item.TotalWeight),
        Number(item.Value),
        Number(item.TotalValue),
        Flag(item.Legendary),
        Flag(item.Favorite),
        Flag(item.Equipped),
        Flag(item.Quest),
        Flag(item.Vendor),
        item.Price is { } price ? Number(price) : "",
        string.Join(" | ", item.Effects.Select(effect => effect.Text)),
    ];

    private static string Number(IFormattable value) => value.ToString(null, CultureInfo.InvariantCulture);

    private static string Flag(bool value) => value ? "yes" : "no";

    private static void AppendRow(StringBuilder text, IEnumerable<string> cells) =>
        text.Append(string.Join(',', cells.Select(Escape))).Append("\r\n");

    private static string Escape(string cell)
    {
        if (cell.Length > 0 && "=+-@\t\r".Contains(cell[0]) && !IsNumber(cell))
        {
            cell = "'" + cell;
        }

        return cell.AsSpan().IndexOfAny(",\"\r\n") >= 0 ? $"\"{cell.Replace("\"", "\"\"")}\"" : cell;
    }

    private static bool IsNumber(string cell) =>
        double.TryParse(cell, NumberStyles.Float, CultureInfo.InvariantCulture, out _);
}
