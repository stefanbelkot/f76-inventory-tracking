using InventoryTracking.Server.Api.Contracts;
using InventoryTracking.Server.Security;

namespace InventoryTracking.Server.Hosting;

/// <summary>
/// Turns an unexpected failure of an API call into a JSON answer the page can show, and logs it. The most common failure
/// of a fresh web installation is a session folder the application pool identity may not write to; the answer then says so
/// (without naming the server's folders to the visitor).
/// </summary>
public static class ApiErrors
{
    public const string StorageMessage =
        "The server cannot store session data. The site administrator needs to give the application pool write access to the session folder.";

    public const string UnexpectedMessage = "The server could not complete the request.";

    public static IApplicationBuilder UseApiErrors(this IApplicationBuilder app, ILogger logger) =>
        app.Use(async (context, next) =>
        {
            try
            {
                await next();
            }
            catch (Exception ex) when (LocalRequestGuard.IsApi(context.Request.Path) && !context.Response.HasStarted && ex is not OperationCanceledException)
            {
                var storage = ex is IOException or UnauthorizedAccessException;
                logger.LogError(ex, "API call {Path} failed{Storage}", context.Request.Path, storage ? " (session storage)" : "");
                context.Response.StatusCode = storage ? StatusCodes.Status503ServiceUnavailable : StatusCodes.Status500InternalServerError;
                await context.Response.WriteAsJsonAsync(new ErrorResponse(storage ? StorageMessage : UnexpectedMessage));
            }
        });
}
