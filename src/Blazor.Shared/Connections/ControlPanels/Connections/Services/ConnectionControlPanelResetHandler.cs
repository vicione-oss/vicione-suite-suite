using Blazor.Shared.Connections.Contracts;
using Blazor.Shared.Connections.Factories;
using Blazor.Shared.Connections.Services;
using Sdk.Client.ControlPanels.Services;
using Sdk.Connections.Contracts;

namespace Blazor.Shared.Connections.ControlPanels.Connections.Services;

internal sealed class ConnectionControlPanelResetHandler(ISuiteConnectionService suiteConnectionService, IConnectionTypeRegistry connectionTypeRegistry)
    : IControlPanelResetHandler<ConnectionControlPanelState>
{
    public async Task Reset(ConnectionControlPanelState state, CancellationToken cancellationToken)
    {
        state.BeginLoading();
        try
        {
            if (state.ConnectionId is not null)
            {
                var connection = await suiteConnectionService.GetConnection(state.ConnectionId.Value, cancellationToken)
                    ?? throw new InvalidOperationException($"Connection with id {state.ConnectionId.Value} was not found.");

                state.EditConnectionModel = new EditConnectionModel(connection, connectionTypeRegistry);
            }
            else
            {
                state.EditConnectionModel = EditConnectionModelFactory.CreateNew(connectionTypeRegistry);
            }

            state.TagValues = [.. state.EditConnectionModel.Tags.Select(t => t.Text)];

            state.AvailableTags = await suiteConnectionService.GetTags(cancellationToken);
            state.AvailableTagTexts = [.. state.AvailableTags.Select(t => t.Text)];
        }
        finally
        {
            state.EndLoading();
        }
    }
}
