using InventoryTracking.Core.Setup;
using InventoryTracking.Server.Api.Contracts;
using InventoryTracking.Server.Machine;
using InventoryTracking.Server.Settings;

namespace InventoryTracking.Server.Api;

internal static class SetupEndpoints
{
    public static void Map(RouteGroupBuilder api)
    {
        var setup = api.MapGroup("/setup");

        setup.MapGet("/candidates", (ILocalMachine machine) =>
            new CandidatesResponse(ModsDataCandidates.Find(machine.SteamPath, machine.FixedDriveRoots).ToList()));

        setup.MapGet("/dirs", (string? path, ILocalMachine machine) =>
            DirectoryBrowser.List(path, machine.FixedDriveRoots) is { } listing
                ? Results.Ok(listing)
                : Results.BadRequest(new ErrorResponse("That folder cannot be listed.")));

        setup.MapPost("/folder", (FolderRequest request, FolderSession session) =>
        {
            var resolution = GameFolderResolver.Resolve(request.Path);
            if (resolution is null)
            {
                return Results.BadRequest(new ErrorResponse("No Fallout 76 modsdata folder was found there."));
            }

            if (request.Confirm)
            {
                session.Add(resolution.Path);
            }

            return Results.Ok(new FolderResponse(resolution.Path, resolution.Kind, resolution.ExportFiles, request.Confirm));
        });

        setup.MapPost("/folder/remove", (FolderRemoveRequest request, FolderSession session) =>
        {
            if (LocalPath.Normalize(request.Path) is not { } path)
            {
                return Results.BadRequest(new ErrorResponse("That is not a folder path."));
            }

            session.Remove(path);
            return Results.Ok(new { folders = session.Folders });
        });
    }
}
