using InventoryTracking.Server.Api.Contracts;
using InventoryTracking.Server.Hosting;
using InventoryTracking.Server.Sessions;
using Microsoft.AspNetCore.Http.Features;

namespace InventoryTracking.Server.Api;

/// <summary>Sessions and uploads (web and both mode): create a session, list, upload and delete its files, delete the session.</summary>
internal static class SessionEndpoints
{
    public static void Map(RouteGroupBuilder api)
    {
        api.MapPost("/sessions", (ViewerRuntime runtime) =>
        {
            try
            {
                var id = runtime.Sessions!.Create();
                return Results.Ok(new SessionResponse(id, []));
            }
            catch (SessionLimitException ex)
            {
                return Results.Json(new ErrorResponse(ex.Message), statusCode: StatusCodes.Status503ServiceUnavailable);
            }
        });

        api.MapGet("/session", (HttpContext http, ViewerRuntime runtime, CatalogAccess access) =>
            access.TryGetSession(http, out var id, out var failure)
                ? Results.Ok(new SessionResponse(id, Files(runtime, id)))
                : failure!);

        api.MapPost("/session/files", async (HttpContext http, ViewerRuntime runtime, CatalogAccess access) =>
        {
            if (!access.TryGetSession(http, out var id, out var failure))
            {
                return failure!;
            }

            var web = runtime.Options.Web;
            // A whole upload may hold as many files as a session may hold; each file is checked on its own.
            if (http.Features.Get<IHttpMaxRequestBodySizeFeature>() is { IsReadOnly: false } limit)
            {
                limit.MaxRequestBodySize = web.MaxFileBytes * Math.Min(web.MaxFilesPerSession, 20) + 64 * 1024;
            }

            if (!http.Request.HasFormContentType)
            {
                return Results.BadRequest(new ErrorResponse("Send the files as multipart/form-data."));
            }

            IFormCollection form;
            try
            {
                form = await http.Request.ReadFormAsync(http.RequestAborted);
            }
            catch (Exception ex) when (ex is InvalidDataException or BadHttpRequestException or IOException)
            {
                return Results.BadRequest(new ErrorResponse("The upload is too large or damaged."));
            }

            var results = new List<UploadResultDto>();
            foreach (var file in form.Files)
            {
                if (file.Length > web.MaxFileBytes)
                {
                    results.Add(new UploadResultDto(Path.GetFileName(file.FileName), false, $"The file is larger than {web.MaxFileBytes / 1024} KB."));
                    continue;
                }

                using var buffer = new MemoryStream((int)file.Length);
                await file.CopyToAsync(buffer, http.RequestAborted);
                var saved = runtime.Sessions!.Save(id, file.FileName, buffer.ToArray());
                results.Add(new UploadResultDto(saved.Name, saved.Accepted, saved.Error));
            }

            if (results.Count == 0)
            {
                return Results.BadRequest(new ErrorResponse("No file was sent."));
            }

            // New files: read the whole session again.
            runtime.SessionCatalogs!.Refresh(id);
            return Results.Ok(new UploadResponse(results, Files(runtime, id)));
        });

        api.MapPost("/session/files/delete", (HttpContext http, DeleteFilesRequest request, ViewerRuntime runtime, CatalogAccess access) =>
        {
            if (!access.TryGetSession(http, out var id, out var failure))
            {
                return failure!;
            }

            if (request.Names is not { Count: > 0 })
            {
                return Results.BadRequest(new ErrorResponse("Name the files to delete."));
            }

            var deleted = runtime.Sessions!.Delete(id, request.Names);
            runtime.SessionCatalogs!.Refresh(id);
            return Results.Ok(new DeleteFilesResponse(deleted, Files(runtime, id)));
        });

        api.MapPost("/session/delete", (HttpContext http, ViewerRuntime runtime, CatalogAccess access) =>
        {
            if (!access.TryGetSession(http, out var id, out var failure))
            {
                return failure!;
            }

            runtime.SessionCatalogs!.Forget(id);
            runtime.Sessions!.DeleteSession(id);
            return Results.Ok(new { deleted = true });
        });
    }

    private static IReadOnlyList<SessionFileDto> Files(ViewerRuntime runtime, string id) =>
        [.. runtime.Sessions!.List(id).Select(file => new SessionFileDto(file.Name, file.Size, file.ModifiedUtc))];
}
