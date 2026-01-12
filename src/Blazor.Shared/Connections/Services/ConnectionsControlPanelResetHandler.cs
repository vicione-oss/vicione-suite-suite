using Sdk.Client.ControlPanels.Services;

namespace Blazor.Shared.Connections.Services;

internal sealed class ConnectionsControlPanelResetHandler(ISuiteConnectionService connectionService) : IControlPanelResetHandler<ConnectionsControlPanelState>
{
    public async Task Reset(ConnectionsControlPanelState state, CancellationToken cancellationToken)
    {
        state.ResetSelectedConnections = true;
        state.ResetSelectedTags = true;

        await connectionService.Initialize(cancellationToken);
        var tags = await connectionService.GetTags(cancellationToken);
        state.Tags = tags.ToDictionary(k => k.Id);

        state.DeletingConnections.Clear();
        state.DeletingTags.Clear();
    }
}
