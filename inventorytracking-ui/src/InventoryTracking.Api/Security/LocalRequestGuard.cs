using Microsoft.AspNetCore.Http;

namespace InventoryTracking.Server.Security;

/// <summary>
/// Protects the server from web pages of other sites (spec 8.5). The local server only answers a local Host header with
/// its own port (defeats DNS rebinding); the web version answers the configured host names (any when none are
/// configured). In both cases every API call needs the custom header that a foreign page cannot add without a CORS
/// preflight, which the server never grants. No CORS headers are sent.
/// </summary>
public static class LocalRequestGuard
{
    public const string ApiPrefix = "/api/v1";
    public const string RequiredHeader = "X-InventoryTracking";
    public const string RequiredHeaderValue = "1";

    public static IApplicationBuilder UseLocalRequestGuard(this IApplicationBuilder app) =>
        app.UseGuard((host, port) => IsLocalHost(host, port));

    /// <param name="allowedHosts">Host names (without port) the web version answers to; empty allows any.</param>
    public static IApplicationBuilder UseWebRequestGuard(this IApplicationBuilder app, IReadOnlyList<string> allowedHosts) =>
        app.UseGuard((host, _) => allowedHosts.Count == 0 || (host.HasValue && allowedHosts.Contains(host.Host, StringComparer.OrdinalIgnoreCase)));

    public static bool IsApi(PathString path) => path.StartsWithSegments(ApiPrefix, StringComparison.OrdinalIgnoreCase);

    public static bool IsLocalHost(HostString host, int serverPort) =>
        host.HasValue
        && (host.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) || host.Host == "127.0.0.1")
        && host.Port == serverPort;

    private static IApplicationBuilder UseGuard(this IApplicationBuilder app, Func<HostString, int, bool> hostAllowed) =>
        app.Use(async (context, next) =>
        {
            if (!hostAllowed(context.Request.Host, context.Connection.LocalPort))
            {
                await Reject(context, "The Host header is not allowed.");
            }
            else if (IsApi(context.Request.Path) && context.Request.Headers[RequiredHeader] != RequiredHeaderValue)
            {
                await Reject(context, $"The {RequiredHeader} header is required.");
            }
            else
            {
                await next();
            }
        });

    private static Task Reject(HttpContext context, string message)
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        context.Response.ContentType = "text/plain; charset=utf-8";
        return context.Response.WriteAsync(message);
    }
}
