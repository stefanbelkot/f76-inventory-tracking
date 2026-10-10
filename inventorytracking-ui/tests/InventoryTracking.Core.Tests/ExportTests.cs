using System.Text;
using System.Text.Json;
using InventoryTracking.Core.Export;
using InventoryTracking.Core.Model;

namespace InventoryTracking.Core.Tests;

public class ExportTests
{
    private static string CsvText(params Item[] items) => Encoding.UTF8.GetString(CsvExporter.Write(items)).TrimStart('﻿');

    [Fact]
    public void Csv_has_a_header_a_byte_order_mark_and_one_line_per_item()
    {
        var bytes = CsvExporter.Write([new Item { Name = "Stimpak", Count = 3, Weight = 0.5, Value = 20, Types = ["aid"] }]);

        Assert.Equal([0xEF, 0xBB, 0xBF], bytes[..3]);
        var lines = Encoding.UTF8.GetString(bytes).TrimStart('﻿').Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(2, lines.Length);
        Assert.StartsWith("character,storage,name,count", lines[0]);
        Assert.Contains("Stimpak,3,aid,0,0,0,0.5,1.5,20,60,no,no,no,no", lines[1]);
    }

    [Fact]
    public void Csv_quotes_special_characters_and_neutralizes_formulas()
    {
        var text = CsvText(new Item { Name = "A \"big\", gun", Effects = [new Effect(1, "x", "¬")] }, new Item { Name = "=HYPERLINK(1)" });

        Assert.Contains("\"A \"\"big\"\", gun\"", text);
        Assert.Contains(",'=HYPERLINK(1),", text);
    }

    [Fact]
    public void Csv_joins_effect_lines_and_keeps_numbers_invariant()
    {
        var text = CsvText(new Item { Name = "X", Count = 1, Weight = 1.25, Effects = [new Effect(1, "One", "¬"), new Effect(2, "Two", "¬¬")] });

        Assert.Contains("One | Two", text);
        Assert.Contains(",1.25,1.25,", text);
    }

    [Fact]
    public void Json_is_an_array_with_effects_and_unescaped_symbols()
    {
        var json = Encoding.UTF8.GetString(JsonExporter.Write([new Item { Name = "Gun¢", Effects = [new Effect(1, "One", "¬")] }]));

        using var document = JsonDocument.Parse(json);
        var item = document.RootElement[0];
        Assert.Equal("Gun¢", item.GetProperty("name").GetString());
        Assert.Equal(1, item.GetProperty("effects")[0].GetProperty("star").GetInt32());
        Assert.Contains("¢", json);
        Assert.False(item.TryGetProperty("searchText", out _));
    }
}
