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

internal static partial class DowngradeWebApiHostBuilder
{
    public static WebApplication Build(WebApplicationBuilder builder, IFileSystem fileSystem, DowngradeWebApiParameters options)
    {
        builder.Services.AddSingleton(fileSystem);
        builder.Services.AddSingleton(Options.Create(options.Instance));
        builder.Services.AddSingleton<EventCallbackRegistry>();
        builder.Services.AddPipeClient(options.HostManagement);

        // The simplest host that can display the errors.
        var host = builder.Build();
        host.UseHttpsRedirection();
        host.UseSecurityHeaders(ContentSecurityPolicy.ForStartupFailurePage(StartupFailurePageStyles.DowngradePage));

        // The page offers two links: reset or exit.
        host.MapGet("/", () => CreateVersionDowngradeDetectedHtml(options.DowngradeInformation.CurrentVersion, options.DowngradeInformation.DataVersion));
        host.MapGet("/reset", () => DowngradeReset(host.Services, options));
        host.MapGet("/exit", () => DowngradeExit(host.Services, options));

        return host;
    }

    /// <summary>
    /// Writes the reset file and restarts the suite.
    /// </summary>
    private static IResult DowngradeReset(IServiceProvider services, DowngradeWebApiParameters options)
    {
        var fileSystem = services.GetRequiredService<IFileSystem>();
        var pipeClient = services.GetRequiredService<IPipeClient>();

        LogResetRequested(options.Logger);

        try
        {
            fileSystem.WriteResetFile(options.Instance);
        }
        catch (Exception ex)
        {
            LogResetFileFailed(options.Logger, ex);
        }

        RestartDelayed(pipeClient, options);

        return Results.Redirect("/");
    }

    private static IResult DowngradeExit(IServiceProvider services, DowngradeWebApiParameters options)
    {
        var pipeClient = services.GetRequiredService<IPipeClient>();

        LogExitRequested(options.Logger);

        RestartDelayed(pipeClient, options);

        return Results.Redirect("/");
    }

    /// <summary>
    /// Triggers the restart delayed, so the redirect reaches the browser before the host goes down.
    /// </summary>
    private static void RestartDelayed(IPipeClient pipeClient, DowngradeWebApiParameters options)
        => Task.Run(async () =>
        {
            try
            {
                await Task.Delay(options.StopApplicationDelayMs);
                LogRestartRequested(options.Logger);

                // Restarts Core.OS immediately.
                await pipeClient.RestartSuite(options.Instance);
            }
            catch (Exception ex)
            {
                LogRestartFailed(options.Logger, ex);
            }
        }).ConfigureAwait(false);

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

    [LoggerMessage(LogLevel.Warning, "Suite reset requested by user because of detected version downgrade")]
    private static partial void LogResetRequested(ILogger logger);

    [LoggerMessage(LogLevel.Error, "Failed to create reset file for downgrade reset operation")]
    private static partial void LogResetFileFailed(ILogger logger, Exception ex);

    [LoggerMessage(LogLevel.Information, "Suite shutdown requested by user because of detected version downgrade")]
    private static partial void LogExitRequested(ILogger logger);

    [LoggerMessage(LogLevel.Information, "Stop delay passed by. Requesting restart by hostmanagement now...")]
    private static partial void LogRestartRequested(ILogger logger);

    [LoggerMessage(LogLevel.Error, "Failed to trigger restart")]
    private static partial void LogRestartFailed(ILogger logger, Exception ex);
}
