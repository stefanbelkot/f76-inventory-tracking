using System.Text.Json;
using InventoryTracking.Core.Model;

namespace InventoryTracking.Core.Decoding;

/// <summary>Decodes one <c>itemdump_*.json</c> file (compressed or plain) into its header and rows.</summary>
public static class ExportFileDecoder
{
    private static readonly JsonDocumentOptions DocumentOptions = new() { MaxDepth = 32 };

    /// <exception cref="ExportDecodeException">The file is damaged, too large or from a newer format.</exception>
    public static DecodedFile Decode(ReadOnlyMemory<byte> content)
    {
        if (content.Length > DecodeLimits.MaxFileBytes)
        {
            throw new ExportDecodeException(IssueKind.TooLarge, $"The file is larger than {DecodeLimits.MaxFileBytes} bytes.");
        }

        using var document = ParseDocument(content);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("data", out var data))
        {
            throw new ExportDecodeException(IssueKind.Invalid, "The file is not an export document (no data field).");
        }

        var header = HeaderReader.Read(root);
        var columns = ReadColumns(root);
        var encoding = HeaderReader.ReadString(root, "encoding");
        return encoding switch
        {
            "zlib+base64" => new DecodedFile(header, DecodeCompressed(root, data, columns)),
            null or "plain" => new DecodedFile(header, RowReader.Read(data, columns)),
            _ => throw new ExportDecodeException(IssueKind.Invalid, $"Unknown encoding '{encoding}'."),
        };
    }

    private static List<Item> DecodeCompressed(JsonElement root, JsonElement data, IReadOnlyList<string> columns)
    {
        if (data.ValueKind != JsonValueKind.String)
        {
            throw new ExportDecodeException(IssueKind.Invalid, "The data field of a compressed file must be a string.");
        }

        var inflated = PayloadInflater.Inflate(data.GetString()!, HeaderReader.ReadLong(root, "rawBytes"));
        using var rows = ParseDocument(inflated);
        return RowReader.Read(rows.RootElement, columns);
    }

    private static List<string> ReadColumns(JsonElement root) =>
        root.TryGetProperty("cols", out var cols) && cols.ValueKind == JsonValueKind.Array
            ? cols.EnumerateArray().Select(c => c.GetString() ?? "").ToList()
            : [];

    private static JsonDocument ParseDocument(ReadOnlyMemory<byte> bytes)
    {
        try
        {
            return JsonDocument.Parse(bytes, DocumentOptions);
        }
        catch (JsonException ex)
        {
            throw new ExportDecodeException(IssueKind.Invalid, $"Not valid JSON: {ex.Message}");
        }
    }
}
