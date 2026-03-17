using Blazor.Shared.Instance.ControlPanels.Repositories.Models;
using Core.Shared.Instance.Requests;
using Sdk.Client.ControlPanels.Services;
using Sdk.Client.Infrastructure;

namespace Blazor.Shared.Instance.ControlPanels.Repositories.Services;

internal class ArtifactRepositoriesControlPanelResetHandler(IUiMediator mediator) : IControlPanelResetHandler<ArtifactRepositoriesControlPanelState>
{
    public async Task Reset(ArtifactRepositoriesControlPanelState state, CancellationToken cancellationToken)
    {
        state.BeginLoading();
        try
        {
            var response = await mediator.Request<GetArtifactRepositories, GetArtifactRepositoriesResponse>(new([]), cancellationToken);
            state.Repositories = [.. response.Repositories.Select(s => new ArtifactRepositoryModel(s))];
        }
        finally
        {
            state.EndLoading();
        }
    }
}
