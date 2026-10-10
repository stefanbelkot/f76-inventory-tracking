using System.Text.Json.Serialization;

namespace InventoryTracking.Core.Model;

/// <summary>
/// The merged parts of one list (account, character, storage and snapshot).
/// Status is <see cref="GroupStatus.MixedSnapshots"/> when the same storage also exists with another snapshot;
/// <see cref="MissingParts"/> and <see cref="Rows"/> versus <see cref="ExpectedRows"/> still tell whether this group is whole.
/// </summary>
public sealed record ItemGroup
{
    public required string Id { get; init; }
    public required string Account { get; init; }
    public required string Character { get; init; }
    public required string Storage { get; init; }
    public required string Snapshot { get; init; }
    public string? Title { get; init; }
    public required GroupStatus Status { get; init; }
    public required int Rows { get; init; }
    public int? ExpectedRows { get; init; }
    public required IReadOnlyList<int> PartsFound { get; init; }
    public required int PartsExpected { get; init; }
    public required IReadOnlyList<int> MissingParts { get; init; }
    public required IReadOnlyList<string> Files { get; init; }

    /// <summary>The folders the files of this list were read from (more than one only when copies live in several).</summary>
    public IReadOnlyList<string> Folders { get; init; } = [];

    /// <summary>The character's currency balances; only the player inventory export carries them.</summary>
    public IReadOnlyList<Currency> Currencies { get; init; } = [];

    public required DateTime CapturedAtUtc { get; init; }
    public required ItemTotals Totals { get; init; }

    [JsonIgnore]
    public IReadOnlyList<Item> Items { get; init; } = [];
}
