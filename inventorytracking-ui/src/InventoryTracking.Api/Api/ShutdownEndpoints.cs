using InventoryTracking.Server.Api.Contracts;
using InventoryTracking.Server.Hosting;

namespace InventoryTracking.Server.Api;

internal static class ShutdownEndpoints
{
    /// <summary>Stops the tool. The answer is sent first, then the shutdown is requested. Local mode only.</summary>
    public static void Map(RouteGroupBuilder api) =>
        api.MapPost("/shutdown", (HttpContext context, ShutdownSignal signal) =>
        {
            context.Response.OnCompleted(() =>
            {
                signal.Request();
                return Task.CompletedTask;
            });
            return new ShutdownResponse(true);
        });
}
