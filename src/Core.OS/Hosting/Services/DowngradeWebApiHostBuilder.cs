using System.IO.Abstractions;
using System.Net.Mime;
using System.Text;
using Core.OS.Hosting.Contracts;
using Core.OS.Hosting.Pages;
using Core.OS.HostManagement;
using Core.OS.HostManagement.Extensions;
using Core.OS.Instance;
using Core.OS.Instance.Extensions;
using Core.OS.Security;
using Core.OS.Security.Extensions;
using Microsoft.Extensions.Options;

namespace Core.OS.Hosting.Services;

internal static class DowngradeWebApiHostBuilder
{
    private static int StopDelay = 1000;

    public static WebApplication Build(WebApplicationBuilder builder, IFileSystem fileSystem, DowngradeWebApiParameters options)
    {
        builder.Services.AddSingleton(fileSystem);
        builder.Services.AddSingleton(options.Logger);
        builder.Services.AddSingleton(Options.Create(options.Instance));
        builder.Services.AddSingleton<EventCallbackRegistry>();
        builder.Services.AddPipeClient(options.HostManagement);

        // create the simplest host possible to display the errors
        var host = builder.Build();
        host.UseHttpsRedirection();
        host.UseSecurityHeaders(ContentSecurityPolicy.ForStartupFailurePage(StartupFailurePageStyles.DowngradePage));

        // we have a page with to options (links -> reset or exit)
        host.MapGet("/", () => CreateVersionDowngradeDetectedHtml(options.DowngradeInformation.CurrentVersion, options.DowngradeInformation.DataVersion));
        host.MapGet("/reset", (HttpContext _) => DowngradeReset(host.Services));
        host.MapGet("/exit", (HttpContext _) => DowngradeExit(host.Services));

        StopDelay = options.StopApplicationDelayMs;

        return host;
    }

    /// <summary>
    /// Trigger application shutdown delayed http request can be finished before stopping
    /// </summary>    
    private static void RestartDelayed(IServiceProvider services)
        => Task.Run(async () =>
        {
            var logger = services.GetRequiredService<Serilog.ILogger>();

            try
            {
                // Get services before delay because we usee scoped http context provider!
                var pipeClient = services.GetRequiredService<IPipeClient>();
                var instanceOptions = services.GetRequiredService<IOptions<InstanceOptions>>();

                await Task.Delay(StopDelay);
                logger.Information("Stop delay passed by. Requesting restart by hostmanagement now...");

                // this will restart the Core.OS immediately
                await pipeClient.RestartSuite(instanceOptions.Value);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Failed to trigger restart");
            }
        }).ConfigureAwait(false);

    /// <summary>
    /// Sets the reset file and stops the application
    /// </summary>
    /// <param name="services"></param>
    private static IResult DowngradeReset(IServiceProvider services)
    {
        var logger = services.GetRequiredService<Serilog.ILogger>();
        logger.Warning("Suite reset requested by user because of detected version downgrade");

        try
        {
            var fileSystem = services.GetRequiredService<IFileSystem>();
            var options = services.GetRequiredService<IOptions<InstanceOptions>>();

            fileSystem.WriteResetFile(options.Value);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Failed to create reset file for downgrade reset operation");
        }

        RestartDelayed(services);

        return Results.Redirect("/");
    }

    /// <summary>
    /// Just stops the application
    /// </summary>
    /// <param name="services"></param>
    private static IResult DowngradeExit(IServiceProvider services)
    {
        var logger = services.GetRequiredService<Serilog.ILogger>();
        logger.Information("Suite shutdown requested by user because of detected version downgrade");

        RestartDelayed(services);

        return Results.Redirect("/");
    }

    private static HtmlResult CreateVersionDowngradeDetectedHtml(string currentVersion, string persistedVersion)
    {
        return new HtmlResult(@$"<!doctype html>
            <html lang=""en"">
            <head>
                <meta charset=""UTF-8"">
                <title>Startup Error</title>
                <style>{StartupFailurePageStyles.DowngradePage}</style>
            </head>
            <body>
                <h1>Version mismatch detected</h1>
                <p>Detected a software downgrade from version <strong>{persistedVersion}</strong> to <strong>{currentVersion}</strong>.<br> 
                The persisted data is not compatible with <strong>{currentVersion}</strong>.<br> 
                You can reset your data to factory settings if you want to continue working with version <strong>{currentVersion}</strong>.</p>
                <p><a href=""/reset"">Reset &amp; Restart</a></p>
                <p><a href=""/exit"">Cancel &amp; Exit</a></p>
            </body>
            </html>");
    }

    private class HtmlResult(string html) : IResult
    {
        public Task ExecuteAsync(HttpContext httpContext)
        {
            httpContext.Response.ContentType = MediaTypeNames.Text.Html;
            httpContext.Response.ContentLength = Encoding.UTF8.GetByteCount(html);
            return httpContext.Response.WriteAsync(html);
        }
    }
}
