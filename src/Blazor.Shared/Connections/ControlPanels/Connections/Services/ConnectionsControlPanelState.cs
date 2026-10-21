using Blazor.Shared.Connections.Contracts;
using Sdk.Client.ControlPanels.Services;
using ViciOne.SourceGenerators.NotifyChanged;

namespace Blazor.Shared.Connections.ControlPanels.Connections.Services;

public sealed partial class ConnectionsControlPanelState : ControlPanelState
{
    [NotifyChanged]
    internal partial bool ResetSelectedConnections { get; set; }

    internal List<EditConnectionModel> Connections { get; set; } = [];

    internal List<EditConnectionModel> DeletingConnections { get; } = [];
}
