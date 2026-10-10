using System.Text.Json;
using System.Text.Json.Serialization;
using InventoryTracking.Core.Types;
using InventoryTracking.Server.Api;
using InventoryTracking.Server.Security;
using InventoryTracking.Server.Ui;
using Microsoft.AspNetCore.Http.Features;

namespace InventoryTracking.Server.Hosting;

/// <summary>The two calls a host makes to turn a <see cref="ViewerRuntime"/> into a running web application.</summary>
public static class ViewerApp
{
    public static IServiceCollection AddViewer(this IServiceCollection services, ViewerRuntime runtime)
    {
        services.ConfigureHttpJsonOptions(json =>
            json.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)));
        services.Configure<FormOptions>(form => form.MultipartBodyLengthLimit = runtime.Options.Web.MaxFileBytes * 25);
        services
            .AddSingleton(ItemTypeTable.Default)
            .AddSingleton(runtime)
            .AddSingleton(runtime.Catalogs);

        // Only what the mode has is registered, so an endpoint for something the mode lacks cannot even be mapped.
        if (runtime.Folders is { } folders)
        {
            services.AddSingleton(folders).AddSingleton(runtime.Settings!);
        }

        if (runtime.Machine is { } machine)
        {
            services.AddSingleton(machine);
        }

        if (runtime.Shutdown is { } shutdown)
        {
            services.AddSingleton(shutdown);
        }

        if (runtime.Options.Sessions)
        {
            services.AddHostedService(_ => new SessionMaintenance(runtime));
        }

        return services;
    }

    /// <summary>Request guard, security headers, the API for the runtime's mode, and the embedded Vue app.</summary>
    public static WebApplication UseViewer(this WebApplication app, ViewerRuntime runtime)
    {
        if (runtime.Options.Loopback)
        {
            app.UseLocalRequestGuard();
        }
        else
        {
            app.UseWebRequestGuard(runtime.Options.Web.AllowedHosts);
        }

        app.UseSecurityHeaders();
        app.UseApiErrors(app.Logger);
        app.MapApi(runtime.Options);
        app.UseEmbeddedUi(runtime.DownloadsFolder);
        return app;
    }
}

/// <summary>Deletes expired session folders and idle memory copies every few minutes while the web application runs.</summary>
internal sealed class SessionMaintenance(ViewerRuntime runtime) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        do
        {
            try
            {
                runtime.Maintain();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // A folder that is in use right now is removed on the next round.
            }
        }
        while (await WaitAsync(timer, stoppingToken));
    }

    private static async Task<bool> WaitAsync(PeriodicTimer timer, CancellationToken token)
    {
        try
        {
            return await timer.WaitForNextTickAsync(token);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }
}
