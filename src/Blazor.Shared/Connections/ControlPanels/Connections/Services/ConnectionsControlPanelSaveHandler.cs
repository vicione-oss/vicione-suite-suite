using Blazor.Shared.Connections.Contracts;
using Blazor.Shared.Connections.Services;
using Sdk.Client.ControlPanels.Models;
using Sdk.Client.ControlPanels.Services;

namespace Blazor.Shared.Connections.ControlPanels.Connections.Services;

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

        return new SaveSuccessResult();
    }
}
