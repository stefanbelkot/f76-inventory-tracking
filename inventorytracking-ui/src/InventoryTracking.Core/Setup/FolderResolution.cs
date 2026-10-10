namespace InventoryTracking.Core.Setup;

public enum FolderKind
{
    ModsData,
    InstallFolder,
    ParentFolder,
    DataFolder,
    ExportFolder,
}

/// <summary>The modsdata folder found for a path the user gave, and how many export files it holds.</summary>
public sealed record FolderResolution(string Path, FolderKind Kind, int ExportFiles);
