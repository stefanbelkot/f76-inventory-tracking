using InventoryTracking.Core.Links;
using InventoryTracking.Core.Model;

namespace InventoryTracking.Core.Tests;

public class SearchLinkTests
{
    [Theory]
    [InlineData("Plasma Gun", "Plasma Gun")]
    [InlineData(".Red Terror", "Red Terror")]
    [InlineData("...Red Terror", "Red Terror")]
    [InlineData("Nuka-Cola Quantum¢", "Nuka-Cola Quantum")]
    [InlineData("Stimpak (8)", "Stimpak")]
    [InlineData("★★ Handmade Rifle ☆", "Handmade Rifle")]
    [InlineData(".  Assaultron   Head ¢ (12)", "Assaultron Head")]
    [InlineData("Wood (Scrap)", "Wood (Scrap)")]
    [InlineData("", "")]
    public void Names_are_cleaned_for_searching(string name, string expected) =>
        Assert.Equal(expected, NameCleaner.Clean(name));

    [Fact]
    public void Google_gets_the_game_name_and_the_wiki_gets_the_plain_terms()
    {
        var links = SearchLinkBuilder.ForTerms("Red Terror");

        Assert.Equal("https://www.google.com/search?q=Fallout%2076%20Red%20Terror", links.GoogleUrl);
        Assert.Equal("https://fallout.fandom.com/wiki/Special:Search?query=Red%20Terror", links.WikiUrl);
    }

    [Fact]
    public void Terms_are_url_encoded()
    {
        var links = SearchLinkBuilder.ForTerms("a&b=c#d");

        Assert.Equal("https://www.google.com/search?q=Fallout%2076%20a%26b%3Dc%23d", links.GoogleUrl);
    }

    [Fact]
    public void A_renamed_premium_item_is_searched_by_its_cleaned_name()
    {
        var links = SearchLinkBuilder.ForItem(new Item { Name = ".Red Terror¢" });

        Assert.Equal("Red Terror", links.Name.Terms);
        Assert.EndsWith("Fallout%2076%20Red%20Terror", links.Name.GoogleUrl);
        Assert.Empty(links.Effects);
    }

    [Fact]
    public void A_legendary_item_gets_a_google_link_per_starred_effect_line()
    {
        var item = new Item
        {
            Name = "Gun",
            Effects = [new Effect(1, "Explosive", "¬"), new Effect(null, "Special", "§"), new Effect(2, "Fast reload", "¬¬")],
        };

        var effects = SearchLinkBuilder.ForItem(item).Effects;

        Assert.Equal([1, 2], effects.Select(e => e.Star));
        Assert.Equal("https://www.google.com/search?q=Fallout%2076%20Fast%20reload", effects[1].GoogleUrl);
    }
}
