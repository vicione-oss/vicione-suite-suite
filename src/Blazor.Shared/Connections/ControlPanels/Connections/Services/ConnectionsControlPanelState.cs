using Blazor.Shared.Connections.Contracts;
using Sdk.Client.ControlPanels.Services;

namespace Blazor.Shared.Connections.ControlPanels.Connections.Services;

public sealed class ConnectionsControlPanelState : ControlPanelState
{
    private bool _resetSelectedConnections;

    internal bool ResetSelectedConnections
    {
        get => _resetSelectedConnections;
        set
        {
            if (value == _resetSelectedConnections)
                return;

            _resetSelectedConnections = value;

            OnPropertyChanged(nameof(ResetSelectedConnections));
        }
    }

    internal List<EditConnectionModel> Connections { get; set; } = [];

    internal List<EditConnectionModel> DeletingConnections { get; } = [];

}
