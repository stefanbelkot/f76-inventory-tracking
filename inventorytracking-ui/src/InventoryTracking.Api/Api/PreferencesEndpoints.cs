using InventoryTracking.Server.Api.Contracts;
using InventoryTracking.Server.Settings;

namespace InventoryTracking.Server.Api;

internal static class PreferencesEndpoints
{
    private const int MaxColumns = 30;
    private const int MaxColumnNameLength = 32;
    private static readonly string[] Themes = ["system", "light", "dark"];

    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/preferences", (SettingsStore store) =>
        {
            var settings = store.Load();
            return new PreferencesDto(settings.Theme, settings.Columns);
        });

        api.MapPut("/preferences", (PreferencesDto preferences, SettingsStore store) =>
        {
            if (Validate(preferences) is { } problem)
            {
                return Results.BadRequest(new ErrorResponse(problem));
            }

            store.Update(settings => settings with { Theme = preferences.Theme, Columns = preferences.Columns });
            return Results.Ok(preferences);
        });
    }

    private static string? Validate(PreferencesDto preferences)
    {
        if (!Themes.Contains(preferences.Theme))
        {
            return "The theme must be system, light or dark.";
        }

        return preferences.Columns is { } columns
            && (columns.Count > MaxColumns || columns.Any(name => string.IsNullOrWhiteSpace(name) || name.Length > MaxColumnNameLength))
            ? "The column list is invalid."
            : null;
    }
}
