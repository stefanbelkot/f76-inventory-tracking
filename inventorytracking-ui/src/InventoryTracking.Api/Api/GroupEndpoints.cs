using InventoryTracking.Core.Catalog;
using InventoryTracking.Server.Api.Contracts;
using InventoryTracking.Server.Hosting;

namespace InventoryTracking.Server.Api;

internal static class GroupEndpoints
{
    public static void Map(RouteGroupBuilder api) =>
        api.MapGet("/groups", (HttpContext http, CatalogAccess access) =>
        {
            if (!access.TryGet(http, out var catalog, out var failure))
            {
                return failure!;
            }

            var current = catalog.Current;
            return Results.Ok(new GroupsResponse(current.Version, CharacterOverview.Build(current)));
        });
}
