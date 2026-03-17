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

    /// <summary>
    /// Sends a command to create a new artifact repository.
    /// </summary>
    Task<IArtifactRepositoryServiceResult> CreateRepository(ArtifactRepositoryModel sourceModel, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a command to update an existing artifact repository.
    /// </summary>
    Task<IArtifactRepositoryServiceResult> UpdateRepository(ArtifactRepositoryModel sourceModel, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a command to delete an artifact repository.
    /// </summary>
    Task<IArtifactRepositoryServiceResult> DeleteRepository(ArtifactRepositoryModel sourceModel, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a command to refresh the authentication token for an artifact repository.
    /// </summary>
    Task<IArtifactRepositoryServiceResult> UpdateRepositoryToken(ArtifactRepositoryModel sourceModel, CancellationToken cancellationToken = default);
}
