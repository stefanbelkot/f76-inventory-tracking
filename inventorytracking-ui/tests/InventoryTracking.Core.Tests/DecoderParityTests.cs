using System.Text.Json;
using InventoryTracking.Core.Catalog;
using InventoryTracking.Core.Decoding;
using InventoryTracking.Core.Model;
using InventoryTracking.Core.Tests.Support;

namespace InventoryTracking.Core.Tests;

/// <summary>The decoded real exports must equal the output of tools/decode_export.py (stored under fixtures/oracle).</summary>
public class DecoderParityTests
{
    private static readonly ExportCatalog Catalog = Fixtures.RealCatalog();

    public static TheoryData<string> OracleFiles()
    {
        var data = new TheoryData<string>();
        foreach (var path in Directory.GetFiles(Fixtures.OracleFolder, "*.json").Order())
        {
            data.Add(Path.GetFileName(path));
        }

        return data;
    }

    [Fact]
    public void Real_export_folder_has_one_group_per_oracle_file_and_no_issues()
    {
        Assert.Empty(Catalog.Issues);
        Assert.Equal(Directory.GetFiles(Fixtures.OracleFolder).Length, Catalog.Groups.Count);
        Assert.All(Catalog.Groups, group => Assert.Equal(GroupStatus.Complete, group.Status));
    }

    [Theory]
    [MemberData(nameof(OracleFiles))]
    public void Group_and_items_equal_the_python_decoder_output(string oracleFile)
    {
        using var oracle = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(Fixtures.OracleFolder, oracleFile)));
        var root = oracle.RootElement;
        var group = Catalog.Groups.Single(g => g.Snapshot == root.GetProperty("snapshot").GetString() && g.Storage == root.GetProperty("storage").GetString());

        Assert.Equal(root.GetProperty("rows").GetInt32(), group.Rows);
        Assert.Equal(root.GetProperty("complete").GetBoolean(), group.Status == GroupStatus.Complete);
        Assert.Equal(root.GetProperty("character").GetString(), group.Character);
        Assert.Equal(root.GetProperty("account").GetString(), group.Account);
        Assert.Equal(root.GetProperty("title").GetString(), group.Title);

        var expectedItems = root.GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(expectedItems.Count, group.Items.Count);
        for (var i = 0; i < expectedItems.Count; i++)
        {
            AssertSameItem(expectedItems[i], group.Items[i]);
        }
    }

    [Fact]
    public void Legendary_counts_and_effect_lines_match_the_oracle()
    {
        var expected = OracleItems().Where(item => item.TryGetProperty("desc", out _)).ToList();
        var actual = Catalog.Items.Where(item => item.Desc is not null).ToList();

        Assert.NotEmpty(actual);
        Assert.Equal(expected.Count, actual.Count);
        Assert.Equal(
            expected.Sum(item => item.GetProperty("effects").GetArrayLength()),
            actual.Sum(item => item.Effects.Count));
        Assert.Equal(
            expected.Count(item => item.GetProperty("legendary").GetBoolean()),
            Catalog.Items.Count(item => item.Legendary));
    }

    private static IEnumerable<JsonElement> OracleItems() =>
        Directory.GetFiles(Fixtures.OracleFolder, "*.json")
            .Select(path => JsonDocument.Parse(File.ReadAllBytes(path)).RootElement.GetProperty("items"))
            .SelectMany(items => items.EnumerateArray());

    private static void AssertSameItem(JsonElement expected, Item actual)
    {
        // The viewer deliberately differs from the Python decoder in two display details: the star markers in a
        // legendary module's name become a star count, and the markers in a description become asterisks.
        var (expectedName, markedStars) = StarMarks.SplitName(expected.GetProperty("name").GetString()!);
        Assert.Equal(expectedName, actual.Name);
        Assert.Equal(expected.GetProperty("count").GetInt64(), actual.Count);
        Assert.Equal(expected.GetProperty("id").GetInt64(), actual.Id);
        Assert.Equal(expected.GetProperty("value").GetInt64(), actual.Value);
        Assert.Equal(expected.GetProperty("equipState").GetInt32(), actual.EquipState);
        Assert.Equal(expected.GetProperty("filter").GetInt64(), actual.Filter);
        Assert.Equal(expected.GetProperty("level").GetInt32(), actual.Level);
        Assert.Equal(expected.GetProperty("health").GetDouble(), actual.Health);
        Assert.Equal(expected.GetProperty("maxHealth").GetDouble(), actual.MaxHealth);
        Assert.Equal(expected.GetProperty("weight").GetDouble(), actual.Weight);
        Assert.Equal(expected.GetProperty("legendary").GetBoolean(), actual.Legendary);
        var expectedStars = expected.GetProperty("stars").GetInt32();
        Assert.Equal(expectedStars > 0 ? expectedStars : markedStars, actual.Stars);
        Assert.Equal(expected.GetProperty("favorite").GetBoolean(), actual.Favorite);
        Assert.Equal(expected.GetProperty("quest").GetBoolean(), actual.Quest);
        Assert.Equal(expected.GetProperty("category").GetInt32(), actual.Category);
        Assert.Equal(OptionalString(expected, "card"), actual.Card);
        Assert.Equal(StarMarks.ToAsterisks(OptionalString(expected, "desc")), actual.Desc);

        var expectedEffects = expected.TryGetProperty("effects", out var effects) ? effects.EnumerateArray().ToList() : [];
        Assert.Equal(expectedEffects.Count, actual.Effects.Count);
        for (var i = 0; i < expectedEffects.Count; i++)
        {
            var effect = expectedEffects[i];
            Assert.Equal(effect.GetProperty("star").ValueKind == JsonValueKind.Null ? null : effect.GetProperty("star").GetInt32(), actual.Effects[i].Star);
            Assert.Equal(effect.GetProperty("text").GetString(), actual.Effects[i].Text);
            Assert.Equal(OptionalString(effect, "marker"), actual.Effects[i].Marker);
        }
    }

    private static string? OptionalString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
