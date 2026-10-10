using InventoryTracking.Server.Security;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;

namespace InventoryTracking.Server.Ui;

/// <summary>Serves the Vue app embedded in this assembly, with a fallback to <c>index.html</c> for client-side routes.</summary>
public static class EmbeddedUi
{
    private const string IndexFile = "index.html";
    private const string ManifestFile = "downloads.json";
    private static readonly FileExtensionContentTypeProvider ContentTypes = new();

    /// <param name="downloadsFolder">The folder behind <c>/downloads</c> (null: none). It is read per request, so it may be added later.</param>
    public static void UseEmbeddedUi(this WebApplication app, string? downloadsFolder = null)
    {
        var files = new ManifestEmbeddedFileProvider(typeof(EmbeddedUi).Assembly, "wwwroot");
        app.UseStaticFiles(new StaticFileOptions { FileProvider = files });
        if (downloadsFolder is not null)
        {
            app.MapGet("/downloads/{**file}", (HttpContext http, string? file) => ServeDownload(http, downloadsFolder, file));
        }

        app.MapFallback(context => ServeIndex(context, files));
    }

    /// <summary>One file of the downloads folder (no sub-path tricks: the provider refuses anything outside the folder). The manifest is never cached; other files are offered as attachments.</summary>
    private static IResult ServeDownload(HttpContext http, string folder, string? file)
    {
        if (string.IsNullOrEmpty(file) || !Directory.Exists(folder))
        {
            return Results.NotFound();
        }

        using var provider = new PhysicalFileProvider(folder);
        var info = provider.GetFileInfo(file);
        if (!info.Exists || info.IsDirectory || info.PhysicalPath is null)
        {
            return Results.NotFound();
        }

        if (!ContentTypes.TryGetContentType(info.Name, out var contentType))
        {
            contentType = "application/octet-stream";
        }

        if (string.Equals(file, ManifestFile, StringComparison.OrdinalIgnoreCase))
        {
            http.Response.Headers.CacheControl = "no-store";
            return Results.File(info.PhysicalPath, "application/json; charset=utf-8");
        }

        return Results.File(info.PhysicalPath, contentType, fileDownloadName: info.Name, enableRangeProcessing: true);
    }

    private static async Task ServeIndex(HttpContext context, IFileProvider files)
    {
        var index = files.GetFileInfo(IndexFile);
        if (LocalRequestGuard.IsApi(context.Request.Path) || !HttpMethods.IsGet(context.Request.Method) || !index.Exists)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        context.Response.ContentType = "text/html; charset=utf-8";
        await using var stream = index.CreateReadStream();
        await stream.CopyToAsync(context.Response.Body, context.RequestAborted);
    }
}