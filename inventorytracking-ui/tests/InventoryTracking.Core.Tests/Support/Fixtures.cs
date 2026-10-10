using InventoryTracking.Core.Catalog;
using InventoryTracking.Core.Sources;
using InventoryTracking.Core.Types;

namespace InventoryTracking.Core.Tests.Support;

public static class Fixtures
{
    public static string RealFolder { get; } = Path.Combine(AppContext.BaseDirectory, "fixtures", "real");

    public static string OracleFolder { get; } = Path.Combine(AppContext.BaseDirectory, "fixtures", "oracle");

    public static ExportCatalog RealCatalog() => CatalogBuilder.Build(ReadFolder(RealFolder), ItemTypeTable.Default, 1);

    public static IReadOnlyList<SourceFile> ReadFolder(string folder)
    {
        using var source = new LocalFolderSource(folder);
        return source.ReadFiles();
    }

    public static SourceFile File(string name, byte[] content, DateTime? writtenUtc = null) =>
        new(name, writtenUtc ?? new DateTime(2026, 10, 4, 12, 0, 0, DateTimeKind.Utc), content, null);

    public static ExportCatalog Catalog(params SourceFile[] files) => CatalogBuilder.Build(files, ItemTypeTable.Default, 1);
}
