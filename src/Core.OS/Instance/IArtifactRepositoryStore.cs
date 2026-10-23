using Core.Shared.Instance.Contracts;
using Sdk.Messaging;

namespace Core.OS.Instance;

/// <summary>
/// Manages the persistent storage of <see cref="ArtifactRepository"/> entries used to locate artifact repositories.
/// </summary>
public interface IArtifactRepositoryStore
{
    /// <summary>
    /// Populates the store from the provided configuration when no repositories have been persisted yet.
    /// Supports both the current <c>ArtifactRepository</c> section and the legacy <c>ModuleApi</c> section
    /// for backwards compatibility. Has no effect if repositories already exist.
    /// </summary>
    Task MigrateConfiguredRepositories(IConfiguration config, ILogger logger, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the persisted repositories listed in <paramref name="repositoryIds"/>, or all of them when it is <see langword="null"/>.
    /// </summary>
    Task<List<ArtifactRepository>> GetRepositories(IReadOnlyCollection<Guid>? repositoryIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes all persisted repositories, effectively resetting the store to an empty state.
    /// </summary>
    Task Clear(CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new repository or updates an existing one matched by <see cref="ArtifactRepository.Id"/>.
    /// </summary>
    Task<CrudAction> CreateOrUpdate(ArtifactRepository source, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes the repositories identified by the provided identifiers.
    /// </summary>
    Task<IReadOnlyCollection<ArtifactRepository>> Delete(IReadOnlyCollection<Guid> repositoryIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// Store a list of repositories, replacing all existing entries.
    /// </summary>
    Task Store(List<ArtifactRepository> repositories, CancellationToken cancellationToken = default);
}
