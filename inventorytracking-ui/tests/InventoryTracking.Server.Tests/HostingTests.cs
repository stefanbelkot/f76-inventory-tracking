using System.Net;
using System.Net.Sockets;
using InventoryTracking.Server.Hosting;
using InventoryTracking.Server.Tests.Support;

namespace InventoryTracking.Server.Tests;

public class HostingTests
{
    private static TcpListener Occupy(int port)
    {
        var listener = new TcpListener(IPAddress.Loopback, port);
        listener.Start();
        return listener;
    }

    [Fact]
    public async Task The_server_listens_on_the_requested_port_on_loopback_only()
    {
        await using var viewer = await TestViewer.StartAsync();

        Assert.Equal($"http://127.0.0.1:{viewer.Port}/", viewer.Http.BaseAddress!.ToString());
        var listening = System.Net.NetworkInformation.IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners()
            .Where(endpoint => endpoint.Port == viewer.Port)
            .ToList();
        Assert.NotEmpty(listening);
        Assert.All(listening, endpoint => Assert.Equal(IPAddress.Loopback, endpoint.Address));
    }

    [Fact]
    public async Task An_occupied_port_makes_the_server_use_the_next_one()
    {
        var first = TestViewer.FreePort(followingFree: 3);
        using var occupied = Occupy(first);

        await using var viewer = await TestViewer.StartAsync(startPort: first);

        Assert.Equal(first + 1, viewer.Port);
        using var response = await viewer.Http.GetAsync("/api/v1/status");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Several_occupied_ports_are_skipped()
    {
        var first = TestViewer.FreePort(followingFree: 3);
        using var one = Occupy(first);
        using var two = Occupy(first + 1);

        await using var viewer = await TestViewer.StartAsync(startPort: first);

        Assert.Equal(first + 2, viewer.Port);
    }

    [Fact]
    public async Task Without_any_free_port_in_the_range_the_start_fails_with_a_clear_message()
    {
        var first = 65535;
        using var occupied = Occupy(first);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => TestViewer.StartAsync(startPort: first));

        Assert.Contains("No free port", error.Message);
    }

    [Fact]
    public async Task The_embedded_page_and_its_client_side_routes_are_served()
    {
        await using var viewer = await TestViewer.StartAsync();

        using var root = await viewer.Http.GetAsync("/");
        using var route = await viewer.Http.GetAsync("/settings/deep");
        var page = await root.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, root.StatusCode);
        Assert.Contains("<div id=\"app\"></div>", page);
        Assert.Equal(page, await route.Content.ReadAsStringAsync());
        Assert.Equal("text/html", root.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task An_embedded_script_asset_is_served_with_a_script_content_type()
    {
        await using var viewer = await TestViewer.StartAsync();
        var page = await viewer.Http.GetStringAsync("/");
        var script = System.Text.RegularExpressions.Regex.Match(page, "src=\"(/assets/[^\"]+\\.js)\"").Groups[1].Value;

        using var response = await viewer.Http.GetAsync(script);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("javascript", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public void Command_line_flags_are_parsed()
    {
        var options = CommandLineOptions.Parse(["--no-browser", "--no-tray", "--folder", "C:\\x", "--settings-dir", "C:\\s", "--port", "9000", "--instance-id", "t"]);

        Assert.True(options.NoBrowser);
        Assert.True(options.NoTray);
        Assert.Equal(["C:\\x"], options.Folders);
        Assert.Equal("C:\\s", options.SettingsDirectory);
        Assert.Equal(9000, options.StartPort);
        Assert.Equal("t", options.InstanceId);
    }

    [Fact]
    public void The_folder_flag_can_be_repeated()
    {
        var options = CommandLineOptions.Parse(["--folder", "C:\\steam", "--folder", "D:\\xbox"]);

        Assert.Equal(["C:\\steam", "D:\\xbox"], options.Folders);
    }

    [Fact]
    public void The_defaults_are_port_8080_and_the_local_app_data_folder()
    {
        var options = CommandLineOptions.Parse([]);

        Assert.Equal(8080, options.StartPort);
        Assert.EndsWith("InventoryTrackingViewer", options.SettingsDirectory);
        Assert.False(options.NoBrowser);
    }

    [Theory]
    [InlineData("--bogus")]
    [InlineData("--port")]
    [InlineData("--port", "abc")]
    [InlineData("--port", "70000")]
    public void Bad_command_lines_are_refused(params string[] args) =>
        Assert.Throws<ArgumentException>(() => CommandLineOptions.Parse(args));
}
