using Blazor.Shared.Network.ControlPanels.NetworkInterface.Extensions;
using Blazor.Shared.Services;
using Core.Shared.HostManagement.Requests;
using Sdk.Client.ControlPanels.Services;
using Sdk.Client.Infrastructure;

namespace Blazor.Shared.Network.ControlPanels.NetworkInterface.Services;

internal sealed class NetworkInterfaceControlPanelResetHandler(IUiMediator mediator, ISystemConfigurationService systemConfigurationService)
    : IControlPanelResetHandler<NetworkInterfaceControlPanelState>
{
    public async Task Reset(NetworkInterfaceControlPanelState state, CancellationToken cancellationToken)
    {
        state.BeginLoading();
        try
        {
            var networkInterface = systemConfigurationService.SystemConfiguration.NetworkInterfaces
                .ElementAtOrDefault(state.NetworkInterfaceIndex);

            if (networkInterface is null)
            {
                state.Reset();
                return;
            }

            state.Name = networkInterface.CommonInformation.Name;
            state.Enabled = networkInterface.CommonInformation.Enabled;

            if (networkInterface.IPv4.DHCPEnabled)
            {
                var getDHCPLeaseResponse = await mediator.Request<GetDHCPLeaseInformation, GetDHCPLeaseInformationResponse>(
                    new GetDHCPLeaseInformation(state.Name), cancellationToken);

                state.DHCPLease = getDHCPLeaseResponse.DHCPLease;
            }
            else
            {
                state.DHCPLease = null;
            }

            var getOriginalPhysicalAddressResponse = await mediator.Request<GetOriginalPhysicalAddress, GetOriginalPhysicalAddressResponse>(
                new GetOriginalPhysicalAddress(state.Name), cancellationToken);

            state.IpAddresses = [];

            state.UpdateIPv4Mode(networkInterface);

            state.UpdateIPv4Details(networkInterface);

            state.OriginalMacAddress = getOriginalPhysicalAddressResponse.OriginalPhysicalAddress;
            state.UpdateMacAddress(networkInterface);

            state.HasUnsavedChanges = false;
        }
        finally
        {
            state.EndLoading();
        }
    }
}
