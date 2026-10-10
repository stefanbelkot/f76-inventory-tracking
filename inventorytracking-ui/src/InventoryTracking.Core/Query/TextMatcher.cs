namespace InventoryTracking.Core.Query;

/// <summary>Case-insensitive search where every word of the query must occur in the text.</summary>
public sealed class TextMatcher
{
    private readonly string[] _words;

    public TextMatcher(string? query)
    {
        _words = (query ?? "")
            .ToLowerInvariant()
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    public bool IsEmpty => _words.Length == 0;

    /// <param name="lowerCaseText">The text, already lower-cased.</param>
    public bool Matches(string lowerCaseText) => _words.All(word => lowerCaseText.Contains(word, StringComparison.Ordinal));
}
