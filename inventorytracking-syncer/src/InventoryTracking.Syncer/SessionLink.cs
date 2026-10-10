using System.Text.RegularExpressions;
using System.Web;

namespace InventoryTracking.Syncer;

/// <summary>Reads a session id out of what the user pastes: the viewer's page address (<c>?session=...</c>) or the bare id.</summary>
public static partial class SessionLink
{
    public const string DefaultServer = "https://f76inventorytracker.bestesoftware.eu/";

    public static bool IsSessionId(string? value) => value is { Length: 32 } && SessionId().IsMatch(value);

    /// <summary>
    /// Accepts a full link (other parameters allowed), or the bare 32-digit hex id, in any case with surrounding spaces.
    /// <paramref name="server"/> is the link's address without query when the text was a link, else null.
    /// </summary>
    public static bool TryParse(string? text, out string id, out string? server)
    {
        id = "";
        server = null;
        var trimmed = text?.Trim().Trim('"');
        if (string.IsNullOrEmpty(trimmed))
        {
            return false;
        }

        if (IsSessionId(trimmed.ToLowerInvariant()))
        {
            id = trimmed.ToLowerInvariant();
            return true;
        }

        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
        {
            return false;
        }

        var candidate = HttpUtility.ParseQueryString(uri.Query)["session"]?.Trim().ToLowerInvariant();
        if (!IsSessionId(candidate))
        {
            return false;
        }

        id = candidate!;
        server = NormalizeServer(uri.GetLeftPart(UriPartial.Path));
        return true;
    }

    /// <summary>The server address with a trailing slash, or null when it is not an acceptable address (https, or plain http only for this machine).</summary>
    public static string? NormalizeServer(string? text)
    {
        if (!Uri.TryCreate(text?.Trim(), UriKind.Absolute, out var uri))
        {
            return null;
        }

        var local = uri.IsLoopback;
        if (uri.Scheme != "https" && !(local && uri.Scheme == "http"))
        {
            return null;
        }

        var path = uri.AbsolutePath.EndsWith('/') ? uri.AbsolutePath : uri.AbsolutePath + "/";
        return $"{uri.Scheme}://{uri.Authority}{path}";
    }

    [GeneratedRegex("^[0-9a-f]{32}$")]
    private static partial Regex SessionId();
}
