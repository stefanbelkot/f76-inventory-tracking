using InventoryTracking.Core.Catalog;
using InventoryTracking.Server.Api.Contracts;
using InventoryTracking.Server.Sessions;

namespace InventoryTracking.Server.Hosting;

/// <summary>
/// Finds the catalog an API call works on. Local mode has one catalog; in web and both mode every call names its session
/// in the <c>X-InventoryTracking-Session</c> header. In both mode a call without a session uses the local folders only.
/// </summary>
public sealed class CatalogAccess(ViewerRuntime runtime)
{
    public const string SessionHeader = "X-InventoryTracking-Session";

    /// <summary>The session id named by the request, or null when there is none or it is malformed.</summary>
    public static string? SessionIdOf(HttpContext http) =>
        http.Request.Headers[SessionHeader].ToString() is { Length: > 0 } id && SessionStore.IsValidId(id) ? id : null;

    /// <summary>The catalog for this request; when there is none, <paramref name="failure"/> is the answer to send.</summary>
    public bool TryGet(HttpContext http, out ExportCatalogService catalog, out IResult? failure)
    {
        catalog = null!;
        failure = null;
        var options = runtime.Options;
        if (options.Sessions && (SessionIdOf(http) is { } id))
        {
            if (runtime.SessionCatalogs!.Get(id) is { } session)
            {
                catalog = session;
                return true;
            }

            failure = Results.NotFound(new ErrorResponse("This session does not exist (it may have expired). Start a new one."));
            return false;
        }

        if (options.Mode == ViewerMode.Web)
        {
            failure = Results.Json(new ErrorResponse("A session is required."), statusCode: StatusCodes.Status401Unauthorized);
            return false;
        }

        catalog = runtime.LocalCatalog!;
        return true;
    }

    /// <summary>The session of a request that must have one (uploads, file lists); otherwise <paramref name="failure"/> is the answer.</summary>
    public bool TryGetSession(HttpContext http, out string id, out IResult? failure)
    {
        id = SessionIdOf(http) ?? "";
        failure = null;
        if (id.Length == 0)
        {
            failure = Results.Json(new ErrorResponse("A session is required."), statusCode: StatusCodes.Status401Unauthorized);
            return false;
        }

        if (!runtime.Sessions!.Touch(id))
        {
            failure = Results.NotFound(new ErrorResponse("This session does not exist (it may have expired). Start a new one."));
            return false;
        }

        return true;
    }
}
