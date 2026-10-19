using Blazor.Shared.Connections.Contracts;
using Blazor.Shared.Connections.Services;
using Sdk.Client.ControlPanels.Services;
using Sdk.Connections.Contracts;

namespace Blazor.Shared.Connections.ControlPanels.Connections.Services;

internal sealed class ConnectionsControlPanelResetHandler(ISuiteConnectionService connectionService, IConnectionTypeRegistry connectionRegistry) : IControlPanelResetHandler<ConnectionsControlPanelState>
{
    public Task Reset(ConnectionsControlPanelState state, CancellationToken cancellationToken)
    {
        state.BeginLoading();
        try
        {
            state.DeletingConnections.Clear();

            var connections = connectionService.Connections.Select(c => new EditConnectionModel(c, connectionRegistry));
            state.Connections = [.. connections];

            // Triggers StateChanged in ConnectionsControlPanel.
            state.ResetSelectedConnections = true;
        }
        finally
        {
            state.EndLoading();
        }

        return Task.CompletedTask;
    }
}
