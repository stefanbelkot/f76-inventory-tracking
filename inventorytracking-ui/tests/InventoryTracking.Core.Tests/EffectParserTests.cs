using InventoryTracking.Core.Decoding;

namespace InventoryTracking.Core.Tests;

public class EffectParserTests
{
    [Fact]
    public void Stars_are_the_number_of_markers_at_the_line_end()
    {
        var effects = EffectParser.Parse("Fast ¬\nFaster ¬¬\nFastest ¬¬¬\n");

        Assert.Equal([1, 2, 3], effects.Select(e => e.Star));
        Assert.Equal(["Fast", "Faster", "Fastest"], effects.Select(e => e.Text));
        Assert.Equal(["¬", "¬¬", "¬¬¬"], effects.Select(e => e.Marker));
    }

    [Fact]
    public void Special_and_unmarked_lines_have_no_star()
    {
        var effects = EffectParser.Parse("Special effect. §\n+10 Carry Weight");

        Assert.All(effects, effect => Assert.Null(effect.Star));
        Assert.Equal("§", effects[0].Marker);
        Assert.Null(effects[1].Marker);
        Assert.Equal("Special effect.", effects[0].Text);
    }

    [Fact]
    public void Carriage_returns_and_blank_lines_are_handled()
    {
        var effects = EffectParser.Parse("One ¬\r\n\r\n  Two ¬¬  \r\n");

        Assert.Equal(2, effects.Count);
        Assert.Equal("Two", effects[1].Text);
        Assert.Equal(2, effects[1].Star);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Missing_description_gives_no_effects(string? desc) => Assert.Empty(EffectParser.Parse(desc));
}
