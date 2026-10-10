namespace InventoryTracking.Core.Sources;

/// <summary>The fixed naming rule for export files: <c>itemdump_&lt;account&gt;_&lt;character&gt;_&lt;storage&gt;_&lt;part&gt;.json</c>.</summary>
public static class ExportFileName
{
    public const string Prefix = "itemdump_";
    public const string Extension = ".json";
    public const string SearchPattern = Prefix + "*" + Extension;

    public static bool IsExportFile(string fileName) =>
        fileName.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase)
        && fileName.EndsWith(Extension, StringComparison.OrdinalIgnoreCase)
        && fileName.IndexOfAny(['\\', '/']) < 0;

    public static int CountIn(string folder)
    {
        try
        {
            return Directory.EnumerateFiles(folder, SearchPattern).Count(path => IsExportFile(Path.GetFileName(path)));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return 0;
        }
    }
}
