using Blazor.Shared.Instance.ControlPanels.Repositories.Models;
using Sdk.Client.ControlPanels.Models;
using Sdk.Client.ControlPanels.Services;

namespace Blazor.Shared.Instance.ControlPanels.Repositories.Services;

internal sealed class ArtifactRepositoryControlPanelSaveHandler(IArtifactRepositoryClientService service) : IControlPanelSaveHandler<ArtifactRepositoryControlPanelState>
{
    public async Task<ISaveResult> Save(ArtifactRepositoryControlPanelState state, CancellationToken cancellationToken)
    {
        if (state.Repository is null)
            throw new InvalidOperationException("No repository source provided");

        if (string.IsNullOrEmpty(state.Repository.Endpoint))
            return new SaveErrorResult(Localization.ArtifactRepositoryControlPanel.EndpointRequired);

        if (!state.Repository.Endpoint.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return new SaveErrorResult(Localization.ArtifactRepositoryControlPanel.EndpointMustBeHttps);

        if (!Uri.IsWellFormedUriString(state.Repository.Endpoint, UriKind.Absolute))
            return new SaveErrorResult(Localization.ArtifactRepositoryControlPanel.EndpointInvalidUrl);

        if (!string.IsNullOrEmpty(state.Repository.TokenEndpoint) && !Uri.IsWellFormedUriString(state.Repository.TokenEndpoint, UriKind.Absolute))
            return new SaveErrorResult(Localization.ArtifactRepositoryControlPanel.TokenEndpointMustBeAbsoluteUri);

        IArtifactRepositoryServiceResult result;

        if (state.RepositoryId.HasValue)
        {
            result = await service.UpdateRepository(state.Repository, cancellationToken);
        }
        else
        {
            result = await service.CreateRepository(state.Repository, cancellationToken);
        }

        if (result is ArtifactRepositoryServiceSuccessResult)
            return new SaveSuccessResult();

        if (result is ArtifactRepositoryServiceErrorResult errorResult)
            return new SaveErrorResult(errorResult.ErrorMessage);

        throw new NotSupportedException("Result type unknown");
    }
}
