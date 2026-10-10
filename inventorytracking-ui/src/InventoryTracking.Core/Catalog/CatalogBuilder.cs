using InventoryTracking.Core.Decoding;
using InventoryTracking.Core.Model;
using InventoryTracking.Core.Sources;
using InventoryTracking.Core.Types;

namespace InventoryTracking.Core.Catalog;

/// <summary>Decodes every source file, merges the parts into groups and collects the problems of damaged files.</summary>
public static class CatalogBuilder
{
    public static ExportCatalog Build(IReadOnlyList<SourceFile> files, ItemTypeTable types, long version)
    {
        var issues = new List<LoadIssue>();
        var parts = new List<DecodedPart>();
        foreach (var file in files)
        {
            if (file.Content is null)
            {
                issues.Add(new LoadIssue(file.Name, IssueKind.Unreadable, file.Error ?? "The file cannot be read."));
                continue;
            }

            try
            {
                parts.Add(new DecodedPart(file, ExportFileDecoder.Decode(file.Content)));
            }
            catch (ExportDecodeException ex)
            {
                issues.Add(new LoadIssue(file.Name, ex.Kind, ex.Message));
            }
        }

        var groups = new GroupMerger(types).Merge(parts, issues);
        return new ExportCatalog(version, groups, issues, files.Count, DateTime.UtcNow);
    }
}
