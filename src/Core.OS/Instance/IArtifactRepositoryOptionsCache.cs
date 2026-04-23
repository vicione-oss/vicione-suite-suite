using Core.Artifacts;

namespace Core.OS.Instance;

public interface IArtifactRepositoryOptionsCache : IArtifactRepositoryOptionsProvider
{
    Task ReloadOptions(IArtifactRepositoryStore repositoryStore, CancellationToken cancellationToken);
}
