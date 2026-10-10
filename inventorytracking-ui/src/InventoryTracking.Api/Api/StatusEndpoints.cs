using InventoryTracking.Core.Sources;
using InventoryTracking.Core.Types;
using InventoryTracking.Server.Api.Contracts;
using InventoryTracking.Server.Hosting;

namespace InventoryTracking.Server.Api;

internal static class StatusEndpoints
{
    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/capabilities", (ViewerRuntime runtime, ItemTypeTable types) =>
        {
            var options = runtime.Options;
            var web = options.Web;
            return new CapabilitiesResponse(
                Mode: options.Mode.ToString().ToLowerInvariant(),
                FolderSetup: options.Folders,
                FolderBrowsing: options.Folders,
                FolderWatching: options.Folders,
                Shutdown: options.Shutdown,
                Version: typeof(StatusEndpoints).Assembly.GetName().Version?.ToString(3) ?? "0",
                ItemTypes: types.Types,
                Import: options.Sessions,
                ImportLimits: options.Sessions
                    ? new ImportLimits(web.MaxFileBytes, web.MaxFilesPerSession, web.MaxSessionBytes, (int)web.SessionLifetime.TotalDays, (int)web.MemoryIdle.TotalMinutes)
                    : null);
        });

        api.MapGet("/status", (HttpContext http, ViewerRuntime runtime, CatalogAccess access) =>
        {
            if (!access.TryGet(http, out var catalog, out var failure))
            {
                return failure!;
            }

            var current = catalog.Current;
            var folders = (runtime.Folders?.Folders ?? [])
                .Select(path => new FolderStatus(path, Directory.Exists(path), ExportFileName.CountIn(path)))
                .ToList();
            var needsSetup = runtime.Options.Mode switch
            {
                ViewerMode.Local => folders.Count == 0,
                ViewerMode.Web => current.FileCount == 0,
                _ => folders.Count == 0 && current.FileCount == 0,
            };
            return Results.Ok(new StatusResponse(
                current.Version,
                folders,
                folders.Count == 0 ? null : runtime.Folders!.Origin,
                needsSetup,
                current.FileCount,
                current.LoadedAtUtc,
                current.Issues));
        });

        api.MapPost("/refresh", (HttpContext http, CatalogAccess access) =>
        {
            if (!access.TryGet(http, out var catalog, out var failure))
            {
                return failure!;
            }

            catalog.Reload();
            return Results.Ok(new { dataVersion = catalog.Current.Version });
        });
    }
}
