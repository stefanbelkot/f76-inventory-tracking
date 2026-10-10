using System.Text.Json;
using InventoryTracking.Core.Model;

namespace InventoryTracking.Core.Decoding;

/// <summary>Reads and validates the header fields of an export document. Unknown fields are ignored.</summary>
internal static class HeaderReader
{
    public static ExportHeader Read(JsonElement root)
    {
        var version = ReadInt(root, "v") ?? DecodeLimits.SupportedVersion;
        if (version > DecodeLimits.SupportedVersion)
        {
            throw new ExportDecodeException(
                IssueKind.NeedsNewerViewer,
                $"The file uses format version {version}; this viewer supports version {DecodeLimits.SupportedVersion}. It needs a newer viewer.");
        }

        if (version < DecodeLimits.SupportedVersion)
        {
            throw new ExportDecodeException(IssueKind.Invalid, $"Invalid format version {version}.");
        }

        return new ExportHeader(
            version,
            ReadString(root, "account") ?? "",
            ReadString(root, "character") ?? "",
            ReadString(root, "storage") ?? ReadString(root, "source") ?? "unknown",
            ReadString(root, "snapshot") ?? ReadString(root, "snap") ?? "?",
            ReadInt(root, "part") ?? 1,
            ReadInt(root, "parts"),
            ReadInt(root, "rows"),
            ReadString(root, "title"),
            ReadCurrencies(root));
    }

    /// <summary>The <c>currencies</c> list; entries that are not objects with a numeric type and amount are skipped.</summary>
    private static List<Currency> ReadCurrencies(JsonElement root)
    {
        var currencies = new List<Currency>();
        if (!root.TryGetProperty("currencies", out var list) || list.ValueKind != JsonValueKind.Array)
        {
            return currencies;
        }

        foreach (var entry in list.EnumerateArray().Take(DecodeLimits.MaxCurrencies))
        {
            if (entry.ValueKind == JsonValueKind.Object
                && ReadInt(entry, "type") is { } type
                && ReadLong(entry, "amount") is { } amount
                && !currencies.Any(existing => existing.Type == type))
            {
                currencies.Add(new Currency(type, CurrencyTable.NameOf(type), amount, ReadLong(entry, "max")));
            }
        }

        return [.. currencies.OrderBy(currency => CurrencyTable.OrderOf(currency.Type))];
    }

    public static long? ReadLong(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var number)
            ? number
            : null;

    public static string? ReadString(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.GetRawText(),
            _ => null,
        };
    }

    private static int? ReadInt(JsonElement root, string name) =>
        ReadLong(root, name) is { } number && number is >= int.MinValue and <= int.MaxValue ? (int)number : null;
}
