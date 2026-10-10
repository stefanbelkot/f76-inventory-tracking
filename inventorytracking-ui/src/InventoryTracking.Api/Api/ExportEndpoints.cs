using InventoryTracking.Core.Export;
using InventoryTracking.Core.Query;
using InventoryTracking.Server.Api.Contracts;
using InventoryTracking.Server.Hosting;

namespace InventoryTracking.Server.Api;

internal static class ExportEndpoints
{
    public static void Map(RouteGroupBuilder api) =>
        api.MapGet("/export", (HttpContext http, string? format, CatalogAccess access) =>
        {
            if (!access.TryGet(http, out var catalog, out var failure))
            {
                return failure!;
            }

            if (!ItemQueryBinder.TryBind(http.Request.Query, out var query, out var error))
            {
                return Results.BadRequest(new ErrorResponse(error!));
            }

            if (ItemQueryEngine.Select(catalog.Current, query) is not { } items)
            {
                return Results.NotFound(new ErrorResponse("Unknown group."));
            }

            return format?.ToLowerInvariant() switch
            {
                "csv" => Results.File(CsvExporter.Write(items), "text/csv; charset=utf-8", "inventory.csv"),
                "json" => Results.File(JsonExporter.Write(items), "application/json; charset=utf-8", "inventory.json"),
                _ => Results.BadRequest(new ErrorResponse("The format must be csv or json.")),
            };
        });
}
