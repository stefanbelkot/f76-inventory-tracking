using System.Text.Json;
using InventoryTracking.Core.Model;

namespace InventoryTracking.Core.Decoding;

/// <summary>Reads the JSON row array. Rows are objects, or arrays zipped with the header's <c>cols</c> names.</summary>
internal static class RowReader
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString,
    };

    public static List<Item> Read(JsonElement rows, IReadOnlyList<string> columns)
    {
        if (rows.ValueKind != JsonValueKind.Array)
        {
            throw new ExportDecodeException(IssueKind.Invalid, "The rows are not a JSON array.");
        }

        var items = new List<Item>(rows.GetArrayLength());
        var index = 0;
        foreach (var row in rows.EnumerateArray())
        {
            index++;
            try
            {
                items.Add(ReadRow(row, columns, index));
            }
            catch (JsonException ex)
            {
                throw new ExportDecodeException(IssueKind.Invalid, $"Row {index} is invalid: {ex.Message}");
            }
        }

        return items;
    }

    private static Item ReadRow(JsonElement row, IReadOnlyList<string> columns, int index)
    {
        var element = row.ValueKind switch
        {
            JsonValueKind.Object => row,
            JsonValueKind.Array when columns.Count > 0 => ZipWithColumns(row, columns),
            _ => throw new ExportDecodeException(IssueKind.Invalid, $"Row {index} is not an object."),
        };
        var item = element.Deserialize<Item>(Options)
            ?? throw new ExportDecodeException(IssueKind.Invalid, $"Row {index} is empty.");
        return item with { Effects = EffectParser.Parse(item.Desc) };
    }

    private static JsonElement ZipWithColumns(JsonElement row, IReadOnlyList<string> columns)
    {
        var values = new Dictionary<string, JsonElement>();
        var position = 0;
        foreach (var cell in row.EnumerateArray())
        {
            if (position >= columns.Count)
            {
                break;
            }

            values[columns[position++]] = cell;
        }

        return JsonSerializer.SerializeToElement(values);
    }
}
