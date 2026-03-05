using Blazor.Shared.Connections.Contracts;
using Blazor.Shared.Connections.Services;
using Sdk.Client.ControlPanels.Services;
using Sdk.Connections.Contracts;

namespace Blazor.Shared.Connections.ControlPanels.Connections.Services;

internal sealed class ConnectionsControlPanelResetHandler(ISuiteConnectionService connectionService, IConnectionTypeRegistry connectionRegistry) : IControlPanelResetHandler<ConnectionsControlPanelState>
{
    public async Task Reset(ConnectionsControlPanelState state, CancellationToken cancellationToken)
    {
        var connections = connectionService.Connections.Select(c => new EditConnectionModel(c, connectionRegistry));
        state.Connections = [.. connections];

        // this will trigger StateChanged in ConnectionsControlPanel
        state.ResetSelectedConnections = true;
        state.ResetSelectedTags = true;

        var tags = await connectionService.GetTags(cancellationToken);
        state.Tags = tags.ToDictionary(k => k.Id);

        state.DeletingConnections.Clear();
        state.DeletingTags.Clear();
    }
}
