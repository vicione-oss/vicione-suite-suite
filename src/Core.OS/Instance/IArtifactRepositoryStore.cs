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
    /// Retrieves all persisted repositories, optionally filtered by their identifiers provided by repositoryIds usage.
    /// </summary>
    /// <param name="repositoryIds">
    /// An optional set of identifiers to filter by. When <see langword="null"/>, all sources are returned.
    /// </param>
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
