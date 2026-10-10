namespace InventoryTracking.Core.Query;

public enum SortField
{
    Name,
    Count,
    Type,
    Level,
    Stars,
    Weight,
    TotalWeight,
    Value,
    TotalValue,
    Price,
    Favorite,
    Equipped,
    Character,
    Storage,
}

/// <summary>A sort column and direction. Text form: <c>name</c> (ascending) or <c>-name</c> (descending).</summary>
public sealed record ItemSort(SortField Field, bool Descending)
{
    /// <exception cref="FormatException">The column name is unknown.</exception>
    public static ItemSort Parse(string text)
    {
        var descending = text.StartsWith('-');
        var name = descending ? text[1..] : text;
        if (!Enum.TryParse<SortField>(name, ignoreCase: true, out var field) || !Enum.IsDefined(field))
        {
            throw new FormatException($"Unknown sort column '{name}'.");
        }

        return new ItemSort(field, descending);
    }
}
