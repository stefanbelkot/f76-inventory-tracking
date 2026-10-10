using System.Text.Json;
using InventoryTracking.Core.Model;

namespace InventoryTracking.Core.Export;

/// <summary>Writes items as an indented JSON array with every field, including the parsed effects.</summary>
public static class JsonExporter
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static byte[] Write(IEnumerable<Item> items) => JsonSerializer.SerializeToUtf8Bytes(items, Options);
}
