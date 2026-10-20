using Blazor.Shared.Network.ControlPanels.NetworkInterface.Extensions;
using Blazor.Shared.Services;
using HostManagement.Shared.Contracts.Network;
using Sdk.Client.Infrastructure;

namespace Blazor.Shared.Network.ControlPanels.NetworkInterface.Services;

internal sealed class NetworkInterfaceGeneralControlPanelResetHandler(IUiMediator mediator, ISystemConfigurationService systemConfigurationService)
    : NetworkInterfaceControlPanelResetHandlerBase(mediator, systemConfigurationService)
{
    protected override async Task Reset(NetworkInterfaceControlPanelState state, NetworkInterfaceDetail networkInterface, CancellationToken cancellationToken)
    {
        await UpdateDHCPLease(state, networkInterface, cancellationToken);
        await UpdateOriginalPhysicalAddress(state, cancellationToken);

        state.Enabled = networkInterface.CommonInformation.Enabled;
    }

    protected override void ResetToDefaults(NetworkInterfaceControlPanelState state)
        => state.ResetGeneral();

    protected override void ClearChanges(NetworkInterfaceControlPanelState state)
        => state.GeneralHasChanges = false;
}
