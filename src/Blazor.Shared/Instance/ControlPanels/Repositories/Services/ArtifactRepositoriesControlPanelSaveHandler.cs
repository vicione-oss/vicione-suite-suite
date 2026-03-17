using Blazor.Shared.Instance.ControlPanels.Repositories.Models;
using Sdk.Client.ControlPanels.Models;
using Sdk.Client.ControlPanels.Services;

namespace Blazor.Shared.Instance.ControlPanels.Repositories.Services;

internal sealed class ArtifactRepositoriesControlPanelSaveHandler(IArtifactRepositoryClientService service) : IControlPanelSaveHandler<ArtifactRepositoriesControlPanelState>
{
    public async Task<ISaveResult> Save(ArtifactRepositoriesControlPanelState state, CancellationToken cancellationToken)
    {
        var reposToDelete = state.RepositoriesMarkedForDeletion.Select(k => k.Id).ToList();
        if (reposToDelete.Count == 0)
            return new SaveSuccessResult();

        foreach (var deletingSource in state.RepositoriesMarkedForDeletion.ToList())
        {
            var result = await service.DeleteRepository(deletingSource, cancellationToken);

            if (result is ArtifactRepositoryServiceErrorResult errorResult)
                return new SaveErrorResult(errorResult.ErrorMessage, errorResult.ErrorCode);

            state.RepositoriesMarkedForDeletion.Remove(deletingSource);
        }

        return new SaveSuccessResult();
    }
}
