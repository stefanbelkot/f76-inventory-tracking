using InventoryTracking.Server.Hosting;

// The web-only viewer for IIS. Settings come from appsettings.json (section "Viewer"); every value has a default.
var builder = WebApplication.CreateBuilder(args);
var settings = builder.Configuration.GetSection("Viewer");
var defaults = new WebOptions();

var web = new WebOptions
{
    SessionsDirectory = settings["SessionsDirectory"] ?? defaults.SessionsDirectory,
    MaxFileBytes = settings.GetValue("MaxFileBytes", defaults.MaxFileBytes),
    MaxFilesPerSession = settings.GetValue("MaxFilesPerSession", defaults.MaxFilesPerSession),
    MaxSessionBytes = settings.GetValue("MaxSessionBytes", defaults.MaxSessionBytes),
    MaxSessions = settings.GetValue("MaxSessions", defaults.MaxSessions),
    MemoryIdle = TimeSpan.FromMinutes(settings.GetValue("MemoryIdleMinutes", defaults.MemoryIdle.TotalMinutes)),
    SessionLifetime = TimeSpan.FromDays(settings.GetValue("SessionLifetimeDays", defaults.SessionLifetime.TotalDays)),
    AllowedHosts = settings.GetSection("AllowedHosts").Get<string[]>() ?? [],
};

// Web only: no local folders, no tray, no shutdown, and answers to the host names of the site (not just loopback).
var runtime = ViewerRuntime.Create(
    new ViewerOptions { Mode = ViewerMode.Web, Loopback = false, Shutdown = false, Web = web },
    applicationDirectory: builder.Environment.ContentRootPath);
runtime.Start();

builder.Services.AddViewer(runtime);
var app = builder.Build();
app.UseViewer(runtime);
if (runtime.StorageProblem is { } problem)
{
    // Visible in the stdout log of the ASP.NET Core Module (set stdoutLogEnabled in web.config) and in the event log.
    app.Logger.LogError("{Problem} Give the application pool identity Modify permission on this folder.", problem);
    Console.Error.WriteLine(problem);
}
app.Lifetime.ApplicationStopping.Register(runtime.Dispose);
app.Run();
