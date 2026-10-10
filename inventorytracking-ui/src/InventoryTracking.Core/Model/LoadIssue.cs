namespace InventoryTracking.Core.Model;

public enum IssueKind
{
    Unreadable,
    Invalid,
    TooLarge,
    NeedsNewerViewer,
    DuplicatePart,
}

/// <summary>A file (or the folder itself, when <paramref name="File"/> is empty) that could not be used.</summary>
public sealed record LoadIssue(string File, IssueKind Kind, string Message);
