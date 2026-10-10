using InventoryTracking.Core.Query;

namespace InventoryTracking.Server.Api;

/// <summary>Reads the item filter, sort and paging parameters shared by the items and export endpoints.</summary>
internal static class ItemQueryBinder
{
    public static bool TryBind(IQueryCollection query, out ItemQuery result, out string? error)
    {
        result = new ItemQuery();
        error = null;
        try
        {
            result = new ItemQuery
            {
                GroupId = Text(query, "group"),
                Character = query.TryGetValue("character", out var character) && character.Count > 0 ? character.ToString() : null,
                Account = query.TryGetValue("account", out var account) && account.Count > 0 ? account.ToString() : null,
                Types = List(query, "type").Select(type => type.ToLowerInvariant()).ToHashSet(),
                Text = Text(query, "q"),
                Legendary = Flag(query, "legendary"),
                Stars = List(query, "stars").Select(star => Number(star, "stars")).ToHashSet(),
                Favorite = Flag(query, "favorite"),
                Equipped = Flag(query, "equipped"),
                Quest = Flag(query, "quest"),
                Vendor = Flag(query, "vendor"),
                Sort = Text(query, "sort") is { } sort ? ItemSort.Parse(sort) : null,
                Page = Text(query, "page") is { } page ? Number(page, "page") : 1,
                PageSize = Text(query, "pageSize") is { } size ? Number(size, "pageSize") : ItemQuery.DefaultPageSize,
            };
            return true;
        }
        catch (FormatException ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private static string? Text(IQueryCollection query, string name) =>
        query.TryGetValue(name, out var values) && values.ToString() is { Length: > 0 } text ? text : null;

    private static IEnumerable<string> List(IQueryCollection query, string name) =>
        query[name]
            .SelectMany(value => value?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? [])
            .Distinct();

    private static bool? Flag(IQueryCollection query, string name) => Text(query, name) switch
    {
        null => null,
        var text when text.Equals("true", StringComparison.OrdinalIgnoreCase) => true,
        var text when text.Equals("false", StringComparison.OrdinalIgnoreCase) => false,
        var text => throw new FormatException($"'{text}' is not true or false ({name})."),
    };

    private static int Number(string text, string name) =>
        int.TryParse(text, out var number) ? number : throw new FormatException($"'{text}' is not a number ({name}).");
}
