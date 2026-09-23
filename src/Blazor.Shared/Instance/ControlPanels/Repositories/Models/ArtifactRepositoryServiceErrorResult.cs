namespace Blazor.Shared.Instance.ControlPanels.Repositories.Models;

/// <summary>
/// Describes the result of a failed call to a method of <see cref="IArtifactRepositoryServiceResult"/>
/// </summary>
public readonly record struct ArtifactRepositoryServiceErrorResult(string ErrorMessage, int? ErrorCode = null)
    : IArtifactRepositoryServiceResult;
