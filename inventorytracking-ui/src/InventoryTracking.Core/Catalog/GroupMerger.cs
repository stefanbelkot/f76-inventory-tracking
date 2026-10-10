using InventoryTracking.Core.Decoding;
using InventoryTracking.Core.Model;
using InventoryTracking.Core.Types;

namespace InventoryTracking.Core.Catalog;

/// <summary>
/// Groups decoded parts by account, character, storage and snapshot, merges the parts in order,
/// and decides whether each group is complete, incomplete or part of mixed snapshots.
/// </summary>
internal sealed class GroupMerger(ItemTypeTable types)
{
    private readonly GroupIdFactory _ids = new();

    public List<ItemGroup> Merge(IEnumerable<DecodedPart> parts, List<LoadIssue> issues)
    {
        var byGroup = parts
            .GroupBy(part => (
                part.Decoded.Header.Account,
                part.Decoded.Header.Character,
                part.Decoded.Header.Storage,
                part.Decoded.Header.Snapshot))
            .OrderBy(group => group.Key.Account, StringComparer.Ordinal)
            .ThenBy(group => group.Key.Character, StringComparer.Ordinal)
            .ThenBy(group => group.Key.Storage, StringComparer.Ordinal)
            .ThenBy(group => group.Key.Snapshot, StringComparer.Ordinal)
            .ToList();
        var mixedStorages = byGroup
            .GroupBy(group => (group.Key.Account, group.Key.Character, group.Key.Storage))
            .Where(storage => storage.Count() > 1)
            .Select(storage => storage.Key)
            .ToHashSet();

        return byGroup
            .Select(group => BuildGroup(
                group.Key.Account,
                group.Key.Character,
                group.Key.Storage,
                group.Key.Snapshot,
                group.ToList(),
                mixedStorages.Contains((group.Key.Account, group.Key.Character, group.Key.Storage)),
                issues))
            .ToList();
    }

    private ItemGroup BuildGroup(
        string account,
        string character,
        string storage,
        string snapshot,
        List<DecodedPart> parts,
        bool mixed,
        List<LoadIssue> issues)
    {
        var byNumber = new SortedDictionary<int, DecodedPart>();
        foreach (var part in parts)
        {
            if (!byNumber.TryAdd(part.Decoded.Header.Part, part))
            {
                issues.Add(new LoadIssue(part.File.Name, IssueKind.DuplicatePart, $"Part {part.Decoded.Header.Part} of this list was already loaded; this copy is ignored."));
            }
        }

        var first = byNumber.Values.First().Decoded.Header;
        var expected = first.Parts ?? byNumber.Count;
        var missing = Enumerable.Range(1, expected).Where(number => !byNumber.ContainsKey(number)).ToList();
        var id = _ids.Create(account, character, storage, snapshot);
        var items = byNumber.Values
            .SelectMany(part => part.Decoded.Items)
            .Select((item, index) => Enrich(item, id, index, character, storage))
            .ToList();
        var whole = missing.Count == 0 && items.Count == (first.Rows ?? items.Count);

        return new ItemGroup
        {
            Id = id,
            Account = account,
            Character = character,
            Storage = storage,
            Snapshot = snapshot,
            Title = first.Title,
            Status = mixed ? GroupStatus.MixedSnapshots : whole ? GroupStatus.Complete : GroupStatus.Incomplete,
            Rows = items.Count,
            ExpectedRows = first.Rows,
            PartsFound = [.. byNumber.Keys],
            PartsExpected = expected,
            MissingParts = missing,
            Files = byNumber.Values.Select(part => part.File.Name).ToList(),
            Folders = byNumber.Values.Select(part => part.File.Origin).OfType<string>().Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            Currencies = byNumber.Values.Select(part => part.Decoded.Header.Currencies).FirstOrDefault(list => list is { Count: > 0 }) ?? [],
            CapturedAtUtc = byNumber.Values.Max(part => part.File.LastWriteUtc),
            Totals = ItemTotals.Of(items),
            Items = items,
        };
    }

    private Item Enrich(Item item, string groupId, int index, string character, string storage)
    {
        // Legendary modules carry their stars as leading markers in the name and have Stars = 0 in the file.
        var (name, markedStars) = StarMarks.SplitName(item.Name);
        var cleaned = item with
        {
            Name = name,
            Stars = item.Stars > 0 ? item.Stars : markedStars,
            Desc = StarMarks.ToAsterisks(item.Desc),
        };
        return cleaned with
        {
            Key = $"{groupId}.{index}",
            GroupId = groupId,
            Character = character,
            Storage = storage,
            Types = types.Classify(item.Filter),
            SearchText = BuildSearchText(cleaned),
        };
    }

    private static string BuildSearchText(Item item) =>
        string.Join('\n', item.Effects.Select(effect => effect.Text).Prepend(item.Name)).ToLowerInvariant();
}
