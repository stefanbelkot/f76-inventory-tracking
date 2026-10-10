using System.Text;
using InventoryTracking.Core.Decoding;
using InventoryTracking.Core.Model;
using InventoryTracking.Core.Tests.Support;

namespace InventoryTracking.Core.Tests;

public class DecoderFailureTests
{
    private static ExportDecodeException Fails(byte[] content) =>
        Assert.Throws<ExportDecodeException>(() => ExportFileDecoder.Decode(content));

    private static byte[] Text(string json) => Encoding.UTF8.GetBytes(json);

    [Fact]
    public void Corrupt_base64_is_reported()
    {
        var file = Text(Encoding.UTF8.GetString(new ExportFileBuilder().RowsOf(2).Build()).Replace("\"data\":\"", "\"data\":\"!!!"));

        var error = Fails(file);

        Assert.Equal(IssueKind.Invalid, error.Kind);
        Assert.Contains("base64", error.Message);
    }

    [Fact]
    public void Valid_base64_that_is_not_zlib_is_reported()
    {
        var file = new ExportFileBuilder().Header("data", Convert.ToBase64String("plain text, not zlib"u8.ToArray())).Build();

        Assert.Equal(IssueKind.Invalid, Fails(file).Kind);
    }

    [Fact]
    public void A_higher_format_version_needs_a_newer_viewer()
    {
        var error = Fails(new ExportFileBuilder().Header("v", 2).RowsOf(1).Build());

        Assert.Equal(IssueKind.NeedsNewerViewer, error.Kind);
        Assert.Contains("newer viewer", error.Message);
    }

    [Fact]
    public void Raw_bytes_over_the_limit_are_rejected_before_inflating()
    {
        var error = Fails(Text("""{"v":1,"encoding":"zlib+base64","rawBytes":16777217,"data":"eJwDAAAAAAE="}"""));

        Assert.Equal(IssueKind.TooLarge, error.Kind);
    }

    [Fact]
    public void Data_that_inflates_beyond_raw_bytes_is_rejected()
    {
        var file = new ExportFileBuilder().RowsOf(50).Header("rawBytes", 100).Build();
        var tampered = Text(Encoding.UTF8.GetString(file));

        Assert.Equal(IssueKind.TooLarge, Fails(tampered).Kind);
    }

    [Fact]
    public void Raw_bytes_that_do_not_match_the_inflated_size_are_rejected()
    {
        var builder = new ExportFileBuilder().RowsOf(3);
        var file = Encoding.UTF8.GetString(builder.Build());
        var real = System.Text.Json.JsonDocument.Parse(file).RootElement.GetProperty("rawBytes").GetInt32();
        var mismatched = file.Replace($"\"rawBytes\":{real}", $"\"rawBytes\":{real + 50}");

        var error = Fails(Text(mismatched));

        Assert.Equal(IssueKind.Invalid, error.Kind);
        Assert.Contains("mismatch", error.Message);
    }

    [Fact]
    public void A_file_over_the_file_size_limit_is_rejected() =>
        Assert.Equal(IssueKind.TooLarge, Fails(new byte[DecodeLimits.MaxFileBytes + 1]).Kind);

    [Theory]
    [InlineData("not json at all")]
    [InlineData("[1,2,3]")]
    [InlineData("""{"v":1,"encoding":"zlib+base64"}""")]
    [InlineData("""{"v":1,"encoding":"rot13","data":"abc"}""")]
    [InlineData("""{"v":0,"data":[]}""")]
    [InlineData("""{"v":1,"data":[1,2]}""")]
    [InlineData("""{"v":1,"data":[{"name":"x","count":"many"}]}""")]
    public void Damaged_documents_are_reported_as_invalid(string json) =>
        Assert.Equal(IssueKind.Invalid, Fails(Text(json)).Kind);

    [Fact]
    public void An_empty_list_decodes_to_no_items()
    {
        var decoded = ExportFileDecoder.Decode(new ExportFileBuilder().Rows().Build());

        Assert.Empty(decoded.Items);
        Assert.Equal(0, decoded.Header.Rows);
    }

    [Fact]
    public void A_plain_file_with_a_data_array_is_accepted()
    {
        var decoded = ExportFileDecoder.Decode(new ExportFileBuilder().Rows(ExportFileBuilder.Row("Plain", 5)).BuildPlain());

        Assert.Equal("Plain", Assert.Single(decoded.Items).Name);
    }

    [Fact]
    public void A_file_without_version_or_encoding_is_read_as_the_old_plain_format()
    {
        var decoded = ExportFileDecoder.Decode(Text("""{"source":"stash","snap":"abc","data":[{"name":"Old","count":2}]}"""));

        Assert.Equal("stash", decoded.Header.Storage);
        Assert.Equal("abc", decoded.Header.Snapshot);
        Assert.Equal(2, decoded.Items[0].Count);
    }

    [Fact]
    public void Array_rows_are_zipped_with_the_column_names()
    {
        var decoded = ExportFileDecoder.Decode(Text("""{"cols":["name","count"],"data":[["Bolt",3]]}"""));

        Assert.Equal(("Bolt", 3L), (decoded.Items[0].Name, decoded.Items[0].Count));
    }

    [Fact]
    public void Unknown_extra_fields_are_ignored()
    {
        var file = new ExportFileBuilder().Header("future", new { a = 1 }).Rows(new { name = "X", unknownColumn = 5, count = 1 }).Build();

        Assert.Equal("X", ExportFileDecoder.Decode(file).Items[0].Name);
    }

    [Fact]
    public void Very_long_names_are_kept_whole()
    {
        var name = new string('x', 5000);

        var decoded = ExportFileDecoder.Decode(new ExportFileBuilder().Rows(ExportFileBuilder.Row(name, 1)).Build());

        Assert.Equal(name, decoded.Items[0].Name);
    }
}
