using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace InventoryTracking.Core.Tests.Support;

/// <summary>Builds synthetic export files in the format of final-spec 3.2, including deliberately broken ones.</summary>
public sealed class ExportFileBuilder
{
    private readonly JsonObject _header = new()
    {
        ["v"] = 1,
        ["tool"] = "InventoryTracking",
        ["account"] = "acc",
        ["character"] = "Hero",
        ["storage"] = "player",
        ["snapshot"] = "aaaa1111",
        ["part"] = 1,
        ["parts"] = 1,
        ["encoding"] = "zlib+base64",
    };

    private List<object> _rows = [];

    public ExportFileBuilder Header(string name, object? value)
    {
        _header[name] = JsonSerializer.SerializeToNode(value);
        return this;
    }

    public ExportFileBuilder Storage(string storage) => Header("storage", storage);

    public ExportFileBuilder Snapshot(string snapshot) => Header("snapshot", snapshot);

    public ExportFileBuilder Part(int part, int parts) => Header("part", part).Header("parts", parts);

    public ExportFileBuilder Rows(params object[] rows)
    {
        _rows = [.. rows];
        return this;
    }

    public ExportFileBuilder RowsOf(int count)
    {
        _rows = Enumerable.Range(1, count).Select(i => (object)Row($"Item {i}", i)).ToList();
        return this;
    }

    public static object Row(string name, long id, long filter = 4, int count = 1, double weight = 1.5, long value = 10) => new
    {
        name,
        count,
        id,
        value,
        equipState = 0,
        filter,
        level = 1,
        health = 100.0,
        maxHealth = 100.0,
        weight,
        legendary = false,
        stars = 0,
        favorite = false,
        quest = false,
        category = 0,
    };

    /// <summary>
    /// The finished file with a compressed <c>data</c> field and a matching <c>rawBytes</c>;
    /// <c>rows</c>, <c>rawBytes</c> and <c>data</c> keep any value set through <see cref="Header"/>.
    /// </summary>
    public byte[] Build()
    {
        var raw = JsonSerializer.SerializeToUtf8Bytes(_rows);
        _header["rows"] ??= _rows.Count;
        _header["rowsInPart"] = _rows.Count;
        _header["rawBytes"] ??= raw.Length;
        _header["data"] ??= Convert.ToBase64String(Compress(raw));
        return Encoding.UTF8.GetBytes(_header.ToJsonString());
    }

    /// <summary>A plain (non-compressed) file whose <c>data</c> field is the row array itself.</summary>
    public byte[] BuildPlain()
    {
        _header["encoding"] = "plain";
        _header["data"] = JsonSerializer.SerializeToNode(_rows);
        return Encoding.UTF8.GetBytes(_header.ToJsonString());
    }

    public static byte[] Compress(byte[] raw)
    {
        using var output = new MemoryStream();
        using (var zlib = new ZLibStream(output, CompressionLevel.SmallestSize))
        {
            zlib.Write(raw);
        }

        return output.ToArray();
    }
}
