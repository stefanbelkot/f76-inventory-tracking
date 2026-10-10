using InventoryTracking.Core.Model;

namespace InventoryTracking.Core.Decoding;

/// <summary>
/// Splits a legendary card description into effect lines. The game ends each effect with one
/// <c>¬</c> per star; <c>§</c> lines and unmarked lines are kept with a null star. Matches decode_export.py.
/// </summary>
public static class EffectParser
{
    private const char StarMarker = '¬';
    private static readonly char[] MarkerChars = [StarMarker, '§', ' '];

    public static IReadOnlyList<Effect> Parse(string? desc)
    {
        if (string.IsNullOrEmpty(desc))
        {
            return [];
        }

        var effects = new List<Effect>();
        foreach (var line in desc.Replace('\r', '\n').Split('\n'))
        {
            var text = line.Trim();
            if (text.Length == 0)
            {
                continue;
            }

            var body = text.TrimEnd(MarkerChars);
            var marker = text[body.Length..].Trim();
            var stars = marker.Count(c => c == StarMarker);
            effects.Add(new Effect(stars > 0 ? stars : null, body, marker.Length > 0 ? marker : null));
        }

        return effects;
    }
}
