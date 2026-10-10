using InventoryTracking.Core.Catalog;
using InventoryTracking.Core.Links;
using InventoryTracking.Core.Query;
using InventoryTracking.Server.Api.Contracts;
using InventoryTracking.Server.Hosting;

namespace InventoryTracking.Server.Api;

internal static class ItemEndpoints
{
    private const int MaxTermsLength = 200;

    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/items", (HttpContext http, CatalogAccess access) =>
        {
            if (!access.TryGet(http, out var catalog, out var failure))
            {
                return failure!;
            }

            if (!ItemQueryBinder.TryBind(http.Request.Query, out var query, out var error))
            {
                return Results.BadRequest(new ErrorResponse(error!));
            }

            return ItemQueryEngine.Run(catalog.Current, query) is { } result
                ? Results.Ok(new ItemsResponse(
                    [.. result.Items.Select(ItemSummary.From)],
                    result.Page,
                    result.PageSize,
                    result.TotalRows,
                    result.Totals))
                : Results.NotFound(new ErrorResponse("Unknown group."));
        });

        api.MapGet("/items/{key}", (HttpContext http, string key, CatalogAccess access) =>
        {
            if (!access.TryGet(http, out var catalog, out var failure))
            {
                return failure!;
            }

            return catalog.Current.FindItem(key) is { } item
                ? Results.Ok(item)
                : Results.NotFound(new ErrorResponse("Unknown item."));
        });

        api.MapGet("/search", (HttpContext http, string? q, CatalogAccess access) =>
            access.TryGet(http, out var catalog, out var failure)
                ? Results.Ok(LocationSearch.Run(catalog.Current, q))
                : failure!);

        api.MapGet("/links", (HttpContext http, string? item, string? q, CatalogAccess access) =>
            access.TryGet(http, out var catalog, out var failure) ? Links(item, q, catalog) : failure!);
    }

    private static IResult Links(string? itemKey, string? terms, ExportCatalogService catalog)
    {
        if (terms is { Length: > MaxTermsLength })
        {
            return Results.BadRequest(new ErrorResponse("The search terms are too long."));
        }

        var effects = (IReadOnlyList<EffectLink>)[];
        SearchLinks name;
        if (itemKey is not null)
        {
            if (catalog.Current.FindItem(itemKey) is not { } item)
            {
                return Results.NotFound(new ErrorResponse("Unknown item."));
            }

            var links = SearchLinkBuilder.ForItem(item);
            effects = links.Effects;
            name = terms is null ? links.Name : SearchLinkBuilder.ForTerms(terms.Trim());
        }
        else if (terms is not null)
        {
            name = SearchLinkBuilder.ForTerms(terms.Trim());
        }
        else
        {
            return Results.BadRequest(new ErrorResponse("Give an item or search terms."));
        }

        return Results.Ok(new ItemLinks(name, effects));
    }
}
