namespace InventoryTracking.Core.Query;

/// <summary>Filters, sort and paging for the item list. Null or empty filters do not restrict the result.</summary>
public sealed record ItemQuery
{
    public const int MaxPageSize = 1000;
    public const int DefaultPageSize = 100;

    /// <summary>Restrict to one group; null searches every group.</summary>
    public string? GroupId { get; init; }

    /// <summary>Restrict to every storage of one character (with <see cref="Account"/>); ignored when <see cref="GroupId"/> is set.</summary>
    public string? Character { get; init; }

    public string? Account { get; init; }

    /// <summary>Item type ids; an item matches when it has any of them.</summary>
    public IReadOnlySet<string> Types { get; init; } = new HashSet<string>();

    /// <summary>Words that must all appear in the name or the effect text.</summary>
    public string? Text { get; init; }

    public bool? Legendary { get; init; }

    /// <summary>Star counts; an item matches when its stars equal any of them.</summary>
    public IReadOnlySet<int> Stars { get; init; } = new HashSet<int>();

    public bool? Favorite { get; init; }
    public bool? Equipped { get; init; }
    public bool? Quest { get; init; }

    /// <summary>True: only items listed at the player's vending machine; false: only the others.</summary>
    public bool? Vendor { get; init; }

    public ItemSort? Sort { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = DefaultPageSize;
}
