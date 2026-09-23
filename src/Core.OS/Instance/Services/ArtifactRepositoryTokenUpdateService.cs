using Core.Artifacts;
using Core.OS.Instance.Extensions;
using Microsoft.Extensions.Options;

namespace Core.OS.Instance.Services;

public partial class ArtifactRepositoryTokenUpdateService(IServiceProvider serviceProvider,
        IOptions<ArtifactRepositoryOptions> options,
        ILogger<ArtifactRepositoryTokenUpdateService> logger) : BackgroundService
{
    private readonly TimeSpan _interval = TimeSpan.FromDays(options.Value.SourceTokenRefreshIntervalDays);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        LogServiceStartedWithIntervalInterval(logger, nameof(ArtifactRepositoryTokenUpdateService), _interval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await UpdateRepositoryTokens(stoppingToken);

                await Task.Delay(_interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Cancellation during shutdown needs no handling.
            }
            catch (Exception ex)
            {
                LogUnhandledExceptionOnUpatingRepositoryTokens(logger, ex);
            }
        }

        LogServiceIsStopping(logger, nameof(ArtifactRepositoryTokenUpdateService));
    }

    internal async Task UpdateRepositoryTokens(CancellationToken cancellationToken)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var repositoryStore = scope.ServiceProvider.GetRequiredService<IArtifactRepositoryStore>();
        var tokenService = scope.ServiceProvider.GetRequiredService<IArtifactRepositoryTokenService>();

        await repositoryStore.UpdateRepositoryTokens(tokenService, logger, cancellationToken);
    }

    [LoggerMessage(LogLevel.Error, "Failed to update repository tokens")]
    static partial void LogFailedToUpdateRepositoryTokens(ILogger<ArtifactRepositoryTokenUpdateService> logger, Exception ex);

    [LoggerMessage(LogLevel.Debug, "{Service} started with interval {Interval}")]
    static partial void LogServiceStartedWithIntervalInterval(ILogger<ArtifactRepositoryTokenUpdateService> logger, string service, TimeSpan interval);

    [LoggerMessage(LogLevel.Information, "Service {Service} is stopping")]
    static partial void LogServiceIsStopping(ILogger<ArtifactRepositoryTokenUpdateService> logger, string service);

    [LoggerMessage(LogLevel.Error, "Unhandled exception on updating repository tokens")]
    static partial void LogUnhandledExceptionOnUpatingRepositoryTokens(ILogger<ArtifactRepositoryTokenUpdateService> logger, Exception ex);
}
