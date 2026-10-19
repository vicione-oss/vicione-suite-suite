using Blazor.Shared.Connections.Services;
using Sdk.Client.ControlPanels.Services;

namespace Blazor.Shared.Connections.ControlPanels.Tags.Services;

internal sealed class ConnectionsTagsControlPanelResetHandler(ISuiteConnectionService connectionService)
    : IControlPanelResetHandler<ConnectionsTagsControlPanelState>
{
    public async Task Reset(ConnectionsTagsControlPanelState state, CancellationToken cancellationToken)
    {
        state.BeginLoading();
        try
        {
            state.DeletingTags.Clear();

            // Triggers StateChanged in ConnectionsTagsControlPanel.
            state.ResetSelectedTags = true;

            var tags = await connectionService.GetTags(cancellationToken);
            state.Tags = tags.ToDictionary(k => k.Id);
        }
        finally
        {
            state.EndLoading();
        }
    }
}
