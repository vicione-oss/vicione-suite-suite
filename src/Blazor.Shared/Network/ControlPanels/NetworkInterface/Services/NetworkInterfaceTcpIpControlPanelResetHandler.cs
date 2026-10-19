using Blazor.Shared.Network.ControlPanels.NetworkInterface.Extensions;
using Blazor.Shared.Services;
using HostManagement.Shared.Contracts.Network;
using Sdk.Client.Infrastructure;

namespace Blazor.Shared.Network.ControlPanels.NetworkInterface.Services;

internal sealed class NetworkInterfaceTcpIpControlPanelResetHandler(IUiMediator mediator, ISystemConfigurationService systemConfigurationService)
    : NetworkInterfaceControlPanelResetHandlerBase(mediator, systemConfigurationService)
{
    protected override async Task Reset(NetworkInterfaceControlPanelState state, NetworkInterfaceDetail networkInterface, CancellationToken cancellationToken)
    {
        await UpdateDHCPLease(state, networkInterface, cancellationToken);

        state.UpdateIPv4Settings(networkInterface);
    }

    protected override void ResetToDefaults(NetworkInterfaceControlPanelState state)
        => state.ResetTcpIp();

    protected override void ClearChanges(NetworkInterfaceControlPanelState state)
        => state.TcpIpHasChanges = false;
}
