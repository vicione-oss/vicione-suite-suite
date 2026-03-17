using Blazor.Shared.Instance.ControlPanels.Repositories.Models;
using Core.Shared.Instance.Contracts;
using Core.Shared.Instance.Requests;
using Sdk.Client.ControlPanels.Services;
using Sdk.Client.Infrastructure;

namespace Blazor.Shared.Instance.ControlPanels.Repositories.Services;

internal class ArtifactRepositoryControlPanelResetHandler(IUiMediator mediator) : IControlPanelResetHandler<ArtifactRepositoryControlPanelState>
{
    public async Task Reset(ArtifactRepositoryControlPanelState state, CancellationToken cancellationToken)
    {
        state.BeginLoading();
        try
        {
            if (state.RepositoryId is null)
            {
                var newRepository = new ArtifactRepository { Id = Guid.NewGuid(), Endpoint = "https://" };
                state.Repository = new ArtifactRepositoryModel(newRepository);
                return;
            }

            var response = await mediator.Request<GetArtifactRepositories, GetArtifactRepositoriesResponse>(new([state.RepositoryId.Value]), cancellationToken);
            var repository = response.Repositories.FirstOrDefault();
            if (repository is null)
            {
                state.Repository = null;
                return;
            }

            state.Repository = new ArtifactRepositoryModel(repository);
        }
        finally
        {
            state.EndLoading();
        }
    }
}
