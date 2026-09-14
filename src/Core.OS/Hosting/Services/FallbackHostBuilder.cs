using Core.OS.EnvironmentOverrides;
using Core.OS.Hosting.Pages;
using Core.OS.HostManagement;
using Core.OS.HostManagement.Extensions;
using Core.OS.Instance;
using Core.OS.Security;
using Core.OS.Security.Extensions;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Core.OS.Hosting.Services;

/// <summary>
/// Builds a minimal web host that stays alive in a degraded state,
/// exposing diagnostic information and preventing service manager restart loops.
/// </summary>
internal static partial class FallbackHostBuilder
{
    public const string DisableEnvironmentOverridesRoute = "/env-overrides/disable";

    public static WebApplication Build(string[] args, FallbackHostOptions options)
    {
        foreach (var message in options.Messages)
        {
            LogFailsafeReason(options.Logger, options.LogLevel, message);
        }

        var builder = WebApplication.CreateBuilder(args);
        AddServices(builder, options);

        var host = builder.Build();
        host.UseHttpsRedirection();
        host.UseSecurityHeaders(ContentSecurityPolicy.ForStartupFailurePage(StartupFailurePageStyles.FailsafePage));
        host.MapHealthChecks("/health");
        MapEndpoints(host, options);

        return host;
    }

    private static void AddServices(WebApplicationBuilder builder, FallbackHostOptions options)
    {
        var description = string.Join("; ", options.Messages);
        builder.Services
            .AddHealthChecks()
            .AddCheck("fallback", () => HealthCheckResult.Unhealthy(description));

        builder.Services.AddRazorComponents();

        if (!CanAct(options))
            return;

        builder.Services.AddSingleton(options.FileSystem!);
        builder.Services.AddSingleton(Options.Create(options.Instance!));
        builder.Services.AddPipeClient(options.HostManagement!);
    }

    private static void MapEndpoints(WebApplication host, FallbackHostOptions options)
    {
        host.MapGet("/", async (CancellationToken cancellationToken) =>
        {
            var diagnostics = await FallbackDiagnosticsFactory.Create(options, cancellationToken);

            return new RazorComponentResult<FailsafePage>(new { Diagnostics = diagnostics })
            {
                StatusCode = options.HttpStatusCode,
            };
        });

        if (!CanAct(options))
            return;

        host.MapPost(DisableEnvironmentOverridesRoute, () => DisableEnvironmentOverrides(host.Services, options));
    }

    /// <summary>
    /// Whether the host was handed enough context to change something and restart afterwards.
    /// </summary>
    private static bool CanAct(FallbackHostOptions options)
        => options.FileSystem is not null && options.Instance is not null && options.HostManagement is not null;

    private static IResult DisableEnvironmentOverrides(IServiceProvider services, FallbackHostOptions options)
    {
        LogDisableRequested(options.Logger);

        try
        {
            var disabledPath = EnvironmentOverridesFile.Disable(options.FileSystem!, options.Instance!.HomeDirectory);
            LogOverridesFileMoved(options.Logger, disabledPath);
        }
        catch (Exception ex)
        {
            LogDisableFailed(options.Logger, ex);
        }

        RestartDelayed(services, options.Logger, options.StopApplicationDelayMs);

        return Results.Redirect("/");
    }

    /// <summary>
    /// Triggers the restart delayed, so the redirect above reaches the browser before the host goes
    /// down - the same reason the downgrade page delays its own restart.
    /// </summary>
    private static void RestartDelayed(IServiceProvider services, ILogger logger, int delayMs)
        => Task.Run(async () =>
        {
            try
            {
                var pipeClient = services.GetRequiredService<IPipeClient>();
                var instanceOptions = services.GetRequiredService<IOptions<InstanceOptions>>();

                await Task.Delay(delayMs);
                LogRestartRequested(logger);

                await pipeClient.RestartSuite(instanceOptions.Value);
            }
            catch (Exception ex)
            {
                LogRestartFailed(logger, ex);
            }
        }).ConfigureAwait(false);

    // The reason is reported through a placeholder rather than as the template itself, so a
    // failure message that happens to contain braces is not read as one.
    [LoggerMessage(Message = "{Reason}")]
    private static partial void LogFailsafeReason(ILogger logger, LogLevel level, string reason);

    [LoggerMessage(LogLevel.Warning, "Runtime environment overrides disabled by operator request from the failsafe debug page")]
    private static partial void LogDisableRequested(ILogger logger);

    [LoggerMessage(LogLevel.Warning, "Moved the environment overrides file to {DisabledPath}")]
    private static partial void LogOverridesFileMoved(ILogger logger, string disabledPath);

    [LoggerMessage(LogLevel.Error, "Failed to disable the environment overrides file")]
    private static partial void LogDisableFailed(ILogger logger, Exception ex);

    [LoggerMessage(LogLevel.Information, "Stop delay passed by. Requesting restart by hostmanagement now...")]
    private static partial void LogRestartRequested(ILogger logger);

    [LoggerMessage(LogLevel.Error, "Failed to trigger restart")]
    private static partial void LogRestartFailed(ILogger logger, Exception ex);
}
