using Blazor.Shared.Instance.ControlPanels.Repositories.Models;
using Core.Shared.Instance.Contracts;
using Sdk.Messaging;

namespace Blazor.Shared.Instance.ControlPanels.Repositories.Services;

/// <summary>
/// Provides client-side operations for managing <see cref="ArtifactRepositoryModel"/> entries,
/// sending commands to the backend and exposing change notifications.
/// </summary>
internal interface IArtifactRepositoryClientService
{
    /// <summary>
    /// Raised when a repository is created, updated, or deleted on the backend.
    /// </summary>
    event Func<ArtifactRepository, CrudAction, Task>? RepositoryChanged;

    Task<IArtifactRepositoryServiceResult> CreateRepository(ArtifactRepositoryModel sourceModel, CancellationToken cancellationToken = default);

    Task<IArtifactRepositoryServiceResult> UpdateRepository(ArtifactRepositoryModel sourceModel, CancellationToken cancellationToken = default);

    Task<IArtifactRepositoryServiceResult> DeleteRepository(ArtifactRepositoryModel sourceModel, CancellationToken cancellationToken = default);

    /// <summary>
    /// Refreshes the authentication token for an artifact repository.
    /// </summary>
    Task<IArtifactRepositoryServiceResult> UpdateRepositoryToken(ArtifactRepositoryModel sourceModel, CancellationToken cancellationToken = default);
}
