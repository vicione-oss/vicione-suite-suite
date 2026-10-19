using Blazor.Shared.Network.ControlPanels.NetworkInterface.Extensions;
using Blazor.Shared.Services;
using HostManagement.Shared.Contracts.Network;
using Sdk.Client.Infrastructure;

namespace Blazor.Shared.Network.ControlPanels.NetworkInterface.Services;

internal sealed class NetworkInterfaceHardwareControlPanelResetHandler(IUiMediator mediator, ISystemConfigurationService systemConfigurationService)
    : NetworkInterfaceControlPanelResetHandlerBase(mediator, systemConfigurationService)
{
    protected override async Task Reset(NetworkInterfaceControlPanelState state, NetworkInterfaceDetail networkInterface, CancellationToken cancellationToken)
    {
        await UpdateOriginalPhysicalAddress(state, cancellationToken);

        state.UpdateMacAddress(networkInterface);
    }

    protected override void ResetToDefaults(NetworkInterfaceControlPanelState state)
        => state.ResetHardware();

    protected override void ClearChanges(NetworkInterfaceControlPanelState state)
        => state.HardwareHasChanges = false;
}
