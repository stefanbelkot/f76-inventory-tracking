namespace InventoryTracking.Core.Decoding;

/// <summary>
/// The game writes star counts as <c>¬</c> characters: one per star at the front of a legendary module's name
/// ("¬¬¬ Durability") and at the end of each effect line. The viewer shows stars as numbers and glyphs instead.
/// </summary>
public static class StarMarks
{
    public const char Marker = '¬';

    /// <summary>Splits "¬¬¬ Durability" into the star count (3) and the clean name ("Durability").</summary>
    public static (string Name, int Stars) SplitName(string name)
    {
        var stars = 0;
        while (stars < name.Length && name[stars] == Marker)
        {
            stars++;
        }

        return stars == 0 ? (name, 0) : (name[stars..].TrimStart(), stars);
    }

    /// <summary>Plain-text form of a description: each star marker becomes an asterisk.</summary>
    public static string? ToAsterisks(string? text) => text?.Replace(Marker, '*');
}
