using Blazor.Shared.Connections.Contracts;
using Blazor.Shared.Connections.Services;
using Sdk.Client.ControlPanels.Models;
using Sdk.Client.ControlPanels.Services;
using Sdk.Connections.Contracts;

namespace Blazor.Shared.Connections.ControlPanels.Tags.Services;

internal sealed class ConnectionsTagsControlPanelSaveHandler(ISuiteConnectionService connectionService)
    : IControlPanelSaveHandler<ConnectionsTagsControlPanelState>
{
    public async Task<ISaveResult> Save(ConnectionsTagsControlPanelState state, CancellationToken cancellationToken)
    {
        foreach (var tag in state.DeletingTags.ToList())
        {
            var deletingTag = new Tag()
            {
                Id = tag.Id,
                Text = tag.Text,
                Protected = tag.Protected
            };

            var result = await connectionService.DeleteTag(deletingTag, cancellationToken);

            if (result is SuiteConnectionServiceErrorResult errorResult)
                return new SaveErrorResult(errorResult.ErrorMessage, errorResult.ErrorCode);

            state.DeletingTags.Remove(deletingTag);
        }

        return new SaveSuccessResult();
    }
}
