namespace InventoryTracking.Core.Decoding;

/// <summary>Size limits that keep a malicious or damaged file from exhausting memory.</summary>
public static class DecodeLimits
{
    public const long MaxRawBytes = 16 * 1024 * 1024;

    public const long MaxFileBytes = 8 * 1024 * 1024;

    public const int SupportedVersion = 1;

    /// <summary>The most currency entries read from a header.</summary>
    public const int MaxCurrencies = 64;
}
