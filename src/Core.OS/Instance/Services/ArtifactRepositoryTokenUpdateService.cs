using Core.Artifacts;
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
                await TryUpdateRepositoryTokens(stoppingToken);

                await Task.Delay(_interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // ignore gracefully
            }
            catch (Exception ex)
            {
                LogUnhandledExceptionOnUpatingRepositoryTokens(logger, ex);
            }
        }

        LogServiceIsStopping(logger, nameof(ArtifactRepositoryTokenUpdateService));
    }

    internal async Task TryUpdateRepositoryTokens(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = serviceProvider.CreateAsyncScope();
            var repositoryStore = scope.ServiceProvider.GetRequiredService<IArtifactRepositoryStore>();
            var tokenService = scope.ServiceProvider.GetRequiredService<IArtifactRepositoryTokenService>();

            // get all repositories and update tokens for those enabled and have token endpoint configured
            var repositories = await repositoryStore.GetRepositories(null, cancellationToken);
            var updateTasks = repositories.Where(r => r.Enabled && !string.IsNullOrEmpty(r.TokenEndpoint))
                .Select(async repo =>
                {
                    try
                    {
                        var tokenResponse = await tokenService.GetToken(new Uri(repo.TokenEndpoint!), cancellationToken);
                        repo.Password = tokenResponse.Token;
                        repo.TokenValidUntil = tokenResponse.ValidUntil;
                        repo.Modified = DateTimeOffset.UtcNow;
                        repo.ModifiedBy = Environment.UserName;
                    }
                    catch (Exception ex)
                    {
                        LogFailedToGetToken(logger, ex, repo.Name, repo.TokenEndpoint);
                    }
                }).ToArray();

            if (updateTasks.Length == 0)
            {
                LogSkipUpdateRepositoryTokens(logger);
                return;
            }

            await Task.WhenAll(updateTasks);

            await repositoryStore.Store(repositories, cancellationToken);
        }
        catch (Exception ex)
        {
            // the whole process is failed, log it and try again in next interval
            LogFailedToUpdateRepositoryTokens(logger, ex);
        }
    }

    [LoggerMessage(LogLevel.Error, "Failed to get token for repository {Name} from '{Endpoint}'")]
    static partial void LogFailedToGetToken(ILogger<ArtifactRepositoryTokenUpdateService> logger, Exception ex, string? name, string? endpoint);

    [LoggerMessage(LogLevel.Error, "Failed to update repository tokens")]
    static partial void LogFailedToUpdateRepositoryTokens(ILogger<ArtifactRepositoryTokenUpdateService> logger, Exception ex);

    [LoggerMessage(LogLevel.Debug, "Skip update repository tokens")]
    static partial void LogSkipUpdateRepositoryTokens(ILogger<ArtifactRepositoryTokenUpdateService> logger);

    [LoggerMessage(LogLevel.Debug, "{Service} started with interval {Interval}")]
    static partial void LogServiceStartedWithIntervalInterval(ILogger<ArtifactRepositoryTokenUpdateService> logger, string service, TimeSpan interval);

    [LoggerMessage(LogLevel.Information, "Service {Service} is stopping")]
    static partial void LogServiceIsStopping(ILogger<ArtifactRepositoryTokenUpdateService> logger, string service);

    [LoggerMessage(LogLevel.Error, "Unhandled exception on updating repository tokens")]
    static partial void LogUnhandledExceptionOnUpatingRepositoryTokens(ILogger<ArtifactRepositoryTokenUpdateService> logger, Exception ex);
}
