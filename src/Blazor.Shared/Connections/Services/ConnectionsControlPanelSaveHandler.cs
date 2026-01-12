using Blazor.Shared.Connections.Contracts;
using Sdk.Client.ControlPanels.Models;
using Sdk.Client.ControlPanels.Services;
using Sdk.Connections.Contracts;

namespace Blazor.Shared.Connections.Services;

internal sealed class ConnectionsControlPanelSaveHandler(ISuiteConnectionService connectionService) : IControlPanelSaveHandler<ConnectionsControlPanelState>
{
    public async Task<ISaveResult> Save(ConnectionsControlPanelState state, CancellationToken cancellationToken)
    {
        foreach (var deletingConnection in state.DeletingConnections.ToList())
        {
            var result = await connectionService.DeleteConnection(deletingConnection.Connection, cancellationToken);

            if (result is SuiteConnectionServiceErrorResult errorResult)
                return new SaveErrorResult(errorResult.ErrorMessage, errorResult.ErrorCode);

            state.DeletingConnections.Remove(deletingConnection);
        }

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
