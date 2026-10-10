using InventoryTracking.Core.Modules;
using InventoryTracking.Server.Api.Contracts;
using InventoryTracking.Server.Hosting;

namespace InventoryTracking.Server.Api;

/// <summary>The junk-mod calculator. The settings (valuable list, prices) live in the browser and travel with each request.</summary>
internal static class ModuleEndpoints
{
    private const int MaxValuable = 500;
    private const int MaxNameLength = 64;

    public static void Map(RouteGroupBuilder api) =>
        api.MapPost("/modules/summary", (HttpContext http, ModuleSummaryRequest request, CatalogAccess access) =>
        {
            if (!access.TryGet(http, out var catalog, out var failure))
            {
                return failure!;
            }

            var valuable = request.Valuable ?? [];
            if (valuable.Count > MaxValuable || valuable.Any(v => string.IsNullOrWhiteSpace(v.Name) || v.Name.Length > MaxNameLength
                || v.Stars is < 1 or > ModuleCalculator.MaxStars))
            {
                return Results.BadRequest(new ErrorResponse("The valuable-mod list is invalid."));
            }

            if (request.LeadersPerModule is < 0 or > 1000)
            {
                return Results.BadRequest(new ErrorResponse("The leaders per module are out of range."));
            }

            var summary = ModuleCalculator.Calculate(
                catalog.Current,
                new ModuleSettings(request.Group, valuable, request.LeadersPerModule));
            return summary is null ? Results.NotFound(new ErrorResponse("Unknown group.")) : Results.Ok(summary);
        });
}

public sealed record ModuleSummaryRequest(
    string? Group,
    IReadOnlyList<ValuableMod>? Valuable,
    decimal LeadersPerModule);
