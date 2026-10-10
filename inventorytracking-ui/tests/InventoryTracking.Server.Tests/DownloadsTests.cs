using System.Net;
using System.Text;
using InventoryTracking.Core.Sources;
using InventoryTracking.Server.Hosting;
using InventoryTracking.Server.Tests.Support;

namespace InventoryTracking.Server.Tests;

public class DownloadsTests : IDisposable
{
    private readonly string _folder = Path.Combine(AppContext.BaseDirectory, "downloads");
    private readonly string _name = $"test-{Guid.NewGuid():N}.zip";

    public DownloadsTests()
    {
        Directory.CreateDirectory(_folder);
        File.WriteAllBytes(Path.Combine(_folder, _name), Encoding.ASCII.GetBytes("PK-test-content"));
        File.WriteAllText(Path.Combine(_folder, "downloads.json"), "{\"files\":[]}");
    }

    public void Dispose()
    {
        File.Delete(Path.Combine(_folder, _name));
        if (!Directory.EnumerateFileSystemEntries(_folder).Any(path => !path.EndsWith("downloads.json", StringComparison.Ordinal)))
        {
            File.Delete(Path.Combine(_folder, "downloads.json"));
        }
    }

    [Fact]
    public async Task A_file_in_the_downloads_folder_is_offered_as_an_attachment_in_web_mode()
    {
        await using var viewer = await TestViewer.StartAsync(withData: false, mode: ViewerMode.Web);

        using var response = await viewer.Http.GetAsync($"/downloads/{_name}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(_name, response.Content.Headers.ContentDisposition?.FileName);
        Assert.Equal("PK-test-content", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task The_manifest_is_json_and_not_cached_and_unknown_or_escaping_paths_are_not_found()
    {
        await using var viewer = await TestViewer.StartAsync(withData: false, mode: ViewerMode.Web);

        using var manifest = await viewer.Http.GetAsync("/downloads/downloads.json");
        using var missing = await viewer.Http.GetAsync("/downloads/nope.zip");
        using var escape = await viewer.Http.GetAsync("/downloads/..%2Fappsettings.json");

        Assert.Equal(HttpStatusCode.OK, manifest.StatusCode);
        Assert.Equal("application/json", manifest.Content.Headers.ContentType?.MediaType);
        Assert.Contains("no-store", manifest.Headers.CacheControl?.ToString());
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.NotEqual(HttpStatusCode.OK, escape.StatusCode);
    }

    [Fact]
    public async Task Local_mode_has_no_downloads()
    {
        await using var viewer = await TestViewer.StartAsync();

        using var response = await viewer.Http.GetAsync($"/downloads/{_name}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}