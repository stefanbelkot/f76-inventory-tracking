using System.Xml.Linq;
using InventoryTracking.Server.Hosting;

namespace InventoryTracking.Server.Tests;

/// <summary>
/// IIS answers 404 itself for any request path that contains a "hidden segment", before the application runs. A hidden
/// segment that equals a segment of an API address therefore breaks that API on IIS (it did once with "sessions").
/// </summary>
public class IisConfigTests
{
    private static string FindWebConfig()
    {
        for (var folder = new DirectoryInfo(AppContext.BaseDirectory); folder is not null; folder = folder.Parent)
        {
            var candidate = Path.Combine(folder.FullName, "src", "InventoryTracking.WebHost", "web.config");
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new FileNotFoundException("The web host's web.config was not found above the test folder.");
    }

    private static IEnumerable<string> HiddenSegments() =>
        XDocument.Load(FindWebConfig()).Descendants("hiddenSegments").Descendants("add").Select(add => (string)add.Attribute("segment")!);

    [Fact]
    public void No_hidden_segment_of_the_web_config_is_part_of_an_api_address_or_the_page()
    {
        string[] used =
        [
            "api", "v1", "capabilities", "status", "refresh", "groups", "items", "export", "search", "links",
            "session", "sessions", "files", "delete", "setup", "preferences", "shutdown", "assets", "index.html",
        ];

        var hidden = HiddenSegments().ToList();

        Assert.NotEmpty(hidden);
        Assert.All(hidden, segment => Assert.DoesNotContain(segment, used, StringComparer.OrdinalIgnoreCase));
    }

    [Fact]
    public void The_default_session_folder_is_the_hidden_segment_and_the_web_host_uses_it()
    {
        var host = File.ReadAllText(Path.Combine(Path.GetDirectoryName(FindWebConfig())!, "appsettings.json"));

        Assert.Contains(WebOptions.DefaultSessionsDirectory, HiddenSegments());
        Assert.Contains($"\"SessionsDirectory\": \"{WebOptions.DefaultSessionsDirectory}\"", host);
    }
}
