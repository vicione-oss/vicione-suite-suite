using Microsoft.Extensions.Diagnostics.HealthChecks;
using Serilog;

namespace Core.OS.Hosting.Services;

/// <summary>
/// Builds a minimal web host that stays alive in a degraded state,
/// exposing diagnostic information and preventing service manager restart loops.
/// </summary>
internal static class FallbackHostBuilder
{
    public static WebApplication Build(string[] args, FallbackHostOptions options)
    {
        foreach (var message in options.Messages)
        {
            Log.Write(options.LogLevel, message);
        }

        var description = string.Join("; ", options.Messages);
        var builder = WebApplication.CreateBuilder(args);
        builder.Services
            .AddHealthChecks()
            .AddCheck("fallback", () => HealthCheckResult.Unhealthy(description));

        var host = builder.Build();
        host.UseHttpsRedirection();
        host.MapHealthChecks("/health");

        host.MapGet("/", () => Results.Json(new
        {
            status = options.Status,
            messages = options.Messages,
            timestamp = DateTimeOffset.UtcNow,
        }, statusCode: options.HttpStatusCode));

        return host;
    }
}
