using Microsoft.AspNetCore.Http;

namespace InventoryTracking.Server.Security;

/// <summary>Response headers that keep the page self-contained: no framing, no foreign scripts, no referrer leaks.</summary>
public static class SecurityHeaders
{
    private const string ContentSecurityPolicy =
        "default-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; frame-ancestors 'none'; base-uri 'none'; form-action 'none'";

    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app) =>
        app.Use((context, next) =>
        {
            var headers = context.Response.Headers;
            headers["Content-Security-Policy"] = ContentSecurityPolicy;
            headers["X-Content-Type-Options"] = "nosniff";
            headers["Referrer-Policy"] = "no-referrer";
            headers["Cache-Control"] = "no-store";
            return next();
        });
}
