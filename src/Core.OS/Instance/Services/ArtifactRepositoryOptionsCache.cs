using Core.Artifacts;
using Core.OS.Instance.Extensions;

namespace Core.OS.Instance.Services;

internal partial class ArtifactRepositoryOptionsCache(IConfiguration configuration, ILogger<ArtifactRepositoryOptionsCache> logger) : IArtifactRepositoryOptionsCache
{
    private readonly Lock _lock = new();
    private ArtifactRepositoryOptions? _options;

    public ArtifactRepositoryOptions GetOptions()
        => _options ?? throw new InvalidOperationException("Options not loaded yet.");

    public async Task ReloadOptions(IArtifactRepositoryStore repositoryStore, IArtifactRepositoryTokenService tokenService, CancellationToken cancellationToken)
    {
        try
        {
            // first update the tokens for the repositories, as they might be outdated
            await repositoryStore.UpdateRepositoryTokens(tokenService, logger, cancellationToken);
        }
        catch (Exception ex)
        {
            LogFailedToUpdateRepositoryTokens(logger, ex);
        }

        // now load the update repositories from the store and update the options
        var repositories = await repositoryStore.GetRepositories(null, cancellationToken);
        var repositoryOptions = configuration.GetSection(ArtifactRepositoryOptions.ConfigSection)
            .Get<ArtifactRepositoryOptions>() ?? new ArtifactRepositoryOptions();

        // We override source configured by environment with the respositories from store,
        // as they are more dynamic and can be updated without restarting the service.
        repositoryOptions.Sources = repositories
                .Where(source => source.Enabled)
                .Select(source => new ArtifactRepositorySourceOption
                {
                    Endpoint = source.Endpoint,
                    UserName = source.UserName,
                    Password = source.Password,
                    TokenEndpoint = source.TokenEndpoint,
                }).ToList();

        lock (_lock) { _options = repositoryOptions; }
    }

    [LoggerMessage(LogLevel.Error, "Failed to update repository tokens on reloading source options")]
    static partial void LogFailedToUpdateRepositoryTokens(ILogger logger, Exception ex);
}
