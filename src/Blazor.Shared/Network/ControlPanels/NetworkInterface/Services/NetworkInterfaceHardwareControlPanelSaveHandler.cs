using Sdk.Client.ControlPanels.Models;
using Sdk.Client.ControlPanels.Services;

namespace Blazor.Shared.Network.ControlPanels.NetworkInterface.Services;

internal sealed class NetworkInterfaceHardwareControlPanelSaveHandler(NetworkInterfaceSaver saver) : IControlPanelSaveHandler<NetworkInterfaceControlPanelState>
{
    public Task<ISaveResult> Save(NetworkInterfaceControlPanelState state, CancellationToken cancellationToken)
        => saver.SaveChanges(state, cancellationToken);
}
