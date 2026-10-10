using InventoryTracking.Core.Model;

namespace InventoryTracking.Core.Decoding;

/// <summary>A file cannot be decoded; <see cref="Kind"/> says why and the message is meant for the user.</summary>
public sealed class ExportDecodeException(IssueKind kind, string message) : Exception(message)
{
    public IssueKind Kind { get; } = kind;
}
