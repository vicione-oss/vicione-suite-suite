using Core.Artifacts;

namespace Core.OS.Instance.Services;

internal class ArtifactRepositoryOptionsCache(IConfiguration configuration) : IArtifactRepositoryOptionsCache
{
    private readonly Lock _lock = new();
    private ArtifactRepositoryOptions? _options;

    public ArtifactRepositoryOptions GetOptions()
        => _options ?? throw new InvalidOperationException("Options not loaded yet.");

    public async Task ReloadOptions(IArtifactRepositoryStore repositoryStore, CancellationToken cancellationToken)
    {
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
                }).ToList();

        lock (_lock) { _options = repositoryOptions; }
    }
}
