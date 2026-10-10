using System.Net;
using InventoryTracking.Server.Security;
using InventoryTracking.Server.Tests.Support;
using Microsoft.AspNetCore.Http;

namespace InventoryTracking.Server.Tests;

public class SecurityTests
{
    private static async Task<HttpResponseMessage> SendWithHeader(TestViewer viewer, HttpMethod method, string path, string? host = null)
    {
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Add("X-InventoryTracking", "1");
        if (host is not null)
        {
            request.Headers.Host = host;
        }

        return await viewer.Http.SendAsync(request);
    }

    [Theory]
    [InlineData("evil.example.com")]
    [InlineData("evil.example.com:PORT")]
    [InlineData("localhost.evil.com:PORT")]
    [InlineData("localhost:1")]
    [InlineData("127.0.0.1")]
    [InlineData("127.0.0.2:PORT")]
    public async Task A_wrong_host_header_is_rejected_for_the_api_and_the_page(string host)
    {
        await using var viewer = await TestViewer.StartAsync();
        host = host.Replace("PORT", viewer.Port.ToString());

        using var api = await SendWithHeader(viewer, HttpMethod.Get, "/api/v1/status", host);
        using var page = await SendWithHeader(viewer, HttpMethod.Get, "/", host);

        Assert.Equal(HttpStatusCode.Forbidden, api.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, page.StatusCode);
    }

    [Theory]
    [InlineData("localhost")]
    [InlineData("127.0.0.1")]
    [InlineData("LOCALHOST")]
    public async Task Local_host_names_with_the_server_port_are_accepted(string name)
    {
        await using var viewer = await TestViewer.StartAsync();

        using var response = await SendWithHeader(viewer, HttpMethod.Get, "/api/v1/status", $"{name}:{viewer.Port}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task An_api_call_without_the_custom_header_is_rejected_but_the_page_is_not()
    {
        await using var viewer = await TestViewer.StartAsync();
        using var plain = new HttpClient { BaseAddress = viewer.Http.BaseAddress };

        using var api = await plain.GetAsync("/api/v1/status");
        using var wrongValue = new HttpRequestMessage(HttpMethod.Get, "/api/v1/status");
        wrongValue.Headers.Add("X-InventoryTracking", "0");
        using var wrong = await plain.SendAsync(wrongValue);
        using var page = await plain.GetAsync("/");

        Assert.Equal(HttpStatusCode.Forbidden, api.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, wrong.StatusCode);
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
    }

    [Fact]
    public async Task A_cors_preflight_is_refused_and_no_cors_headers_are_ever_sent()
    {
        await using var viewer = await TestViewer.StartAsync();
        using var plain = new HttpClient { BaseAddress = viewer.Http.BaseAddress };
        using var preflight = new HttpRequestMessage(HttpMethod.Options, "/api/v1/shutdown");
        preflight.Headers.Add("Origin", "https://evil.example.com");
        preflight.Headers.Add("Access-Control-Request-Method", "POST");
        preflight.Headers.Add("Access-Control-Request-Headers", "x-inventorytracking");

        using var refused = await plain.SendAsync(preflight);
        using var normal = await viewer.Http.GetAsync("/api/v1/status");

        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
        Assert.DoesNotContain(refused.Headers, h => h.Key.StartsWith("Access-Control", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(normal.Headers, h => h.Key.StartsWith("Access-Control", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Responses_carry_the_security_headers()
    {
        await using var viewer = await TestViewer.StartAsync();

        using var response = await viewer.Http.GetAsync("/api/v1/status");

        Assert.Contains("frame-ancestors 'none'", response.Headers.GetValues("Content-Security-Policy").Single());
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.False(response.Headers.Contains("Server"));
    }

    [Fact]
    public async Task Shutdown_without_the_header_or_with_a_wrong_host_does_not_stop_the_viewer()
    {
        await using var viewer = await TestViewer.StartAsync();
        using var plain = new HttpClient { BaseAddress = viewer.Http.BaseAddress };

        using var noHeader = await plain.PostAsync("/api/v1/shutdown", null);
        using var wrongHost = await SendWithHeader(viewer, HttpMethod.Post, "/api/v1/shutdown", "evil.example.com");
        using var get = await viewer.Http.GetAsync("/api/v1/shutdown");
        await Task.Delay(200);

        Assert.Equal(HttpStatusCode.Forbidden, noHeader.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, wrongHost.StatusCode);
        Assert.NotEqual(HttpStatusCode.OK, get.StatusCode);
        Assert.False(viewer.Shutdown.Requested.IsCompleted);
    }

    [Fact]
    public async Task Shutdown_with_the_header_and_a_local_host_answers_and_then_requests_the_stop()
    {
        await using var viewer = await TestViewer.StartAsync();

        using var response = await viewer.Http.PostAsync("/api/v1/shutdown", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("true", await response.Content.ReadAsStringAsync());
        await viewer.Shutdown.Requested.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Theory]
    [InlineData("/api/v1/items/..%2f..%2f..%2fwindows%2fwin.ini")]
    [InlineData("/api/v1/items/%2e%2e%2f%2e%2e")]
    [InlineData("/..%2f..%2f..%2fWindows%2fwin.ini")]
    [InlineData("/%2e%2e/%2e%2e/Windows/win.ini")]
    [InlineData("/assets/..%5c..%5cInventoryTrackingViewer.dll")]
    public async Task Path_traversal_in_the_url_never_reads_a_file(string path)
    {
        await using var viewer = await TestViewer.StartAsync();

        using var response = await viewer.Http.GetAsync(path);
        var body = await response.Content.ReadAsStringAsync();

        Assert.DoesNotContain("[fonts]", body);
        Assert.DoesNotContain("MZ", body[..Math.Min(2, body.Length)]);
        Assert.NotEqual(HttpStatusCode.InternalServerError, response.StatusCode);
    }

    [Theory]
    [InlineData("localhost", 8080, 8080, true)]
    [InlineData("127.0.0.1", 8081, 8081, true)]
    [InlineData("localhost", 8080, 8081, false)]
    [InlineData("example.com", 8080, 8080, false)]
    public void Host_matching_requires_a_local_name_and_the_server_port(string host, int port, int serverPort, bool expected) =>
        Assert.Equal(expected, LocalRequestGuard.IsLocalHost(new HostString(host, port), serverPort));

    [Fact]
    public void A_host_without_a_port_never_matches() =>
        Assert.False(LocalRequestGuard.IsLocalHost(new HostString("localhost"), 8080));
}
