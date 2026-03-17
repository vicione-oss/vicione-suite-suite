using Core.Module;

namespace Core.OS.Instance;

public interface IArtifactRepositoryOptionsCache : IArtifactRepositoryOptionsProvider
{
    Task ReloadOptions(IArtifactRepositoryStore repositoryStore, CancellationToken cancellationToken);
}
