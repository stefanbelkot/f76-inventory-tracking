namespace InventoryTracking.Core.Setup;

/// <summary>Validates paths received from the browser before the backend touches the file system.</summary>
public static class LocalPath
{
    /// <summary>Returns the normalized full path, or null when the text is not an absolute local path.</summary>
    public static string? Normalize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Contains('\0'))
        {
            return null;
        }

        var trimmed = text.Trim().Trim('"');
        if (trimmed.StartsWith(@"\\", StringComparison.Ordinal) || trimmed.StartsWith("//", StringComparison.Ordinal))
        {
            return null;
        }

        try
        {
            return Path.IsPathFullyQualified(trimmed) ? Path.GetFullPath(trimmed) : null;
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }
    }
}
