using InventoryTracking.Core.Model;

namespace InventoryTracking.Core.Links;

/// <summary>Builds search links from fixed templates. Nothing here makes a web request.</summary>
public static class SearchLinkBuilder
{
    public const string GamePrefix = "Fallout 76";

    private const string GoogleTemplate = "https://www.google.com/search?q=";
    private const string WikiTemplate = "https://fallout.fandom.com/wiki/Special:Search?query=";

    /// <summary>Google gets the game name in front of the terms; the wiki searches the terms as given.</summary>
    public static SearchLinks ForTerms(string terms) => new(terms, Google(terms), Wiki(terms));

    public static ItemLinks ForItem(Item item)
    {
        var effects = item.Effects
            .Where(effect => effect.Star is not null)
            .Select(effect => new EffectLink(effect.Star!.Value, effect.Text, Google(effect.Text)))
            .ToList();
        return new ItemLinks(ForTerms(NameCleaner.Clean(item.Name)), effects);
    }

    private static string Google(string terms) => GoogleTemplate + Uri.EscapeDataString($"{GamePrefix} {terms}".Trim());

    private static string Wiki(string terms) => WikiTemplate + Uri.EscapeDataString(terms);
}
