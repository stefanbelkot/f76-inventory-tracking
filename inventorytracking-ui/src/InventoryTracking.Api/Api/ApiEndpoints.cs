using InventoryTracking.Server.Hosting;
using InventoryTracking.Server.Security;

namespace InventoryTracking.Server.Api;

public static class ApiEndpoints
{
    /// <summary>
    /// Maps every <c>/api/v1</c> endpoint the mode allows: folder setup and saved preferences with local folders,
    /// sessions and uploads with the web side, shutdown only when the host can quit.
    /// </summary>
    public static void MapApi(this IEndpointRouteBuilder app, ViewerOptions options)
    {
        var api = app.MapGroup(LocalRequestGuard.ApiPrefix);
        StatusEndpoints.Map(api);
        GroupEndpoints.Map(api);
        ItemEndpoints.Map(api);
        ExportEndpoints.Map(api);
        ModuleEndpoints.Map(api);
        if (options.Folders)
        {
            SetupEndpoints.Map(api);
            PreferencesEndpoints.Map(api);
        }

        if (options.Sessions)
        {
            SessionEndpoints.Map(api);
        }

        if (options.Shutdown)
        {
            ShutdownEndpoints.Map(api);
        }

        api.MapFallback(() => Results.NotFound());
    }
}
