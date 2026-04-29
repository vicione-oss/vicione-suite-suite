using Blazor.Shared.Network.ControlPanels.NetworkInterface.Extensions;
using Core.Shared.HostManagement.Requests;
using Core.Shared.HostManagement.Services;
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
            var networkInterface = systemConfigurationService.SystemConfiguration.NetworkInterfacesSettings.NetworkInterfaces
                .ElementAtOrDefault(state.NetworkInterfaceIndex);

            if (networkInterface is null)
            {
                state.Reset();
                return;
            }

            state.Name = networkInterface.CommonInformation.Name;
            state.Enabled = networkInterface.CommonInformation.Enabled;

            var getDHCPLeaseResponse = await mediator.Request<GetDHCPLeaseInformation, GetDHCPLeaseInformationResponse>(
                    new GetDHCPLeaseInformation(state.Name), cancellationToken);

            var getOriginalPhysicalAddressResponse = await mediator.Request<GetOriginalPhysicalAddress, GetOriginalPhysicalAddressResponse>(
                new GetOriginalPhysicalAddress(state.Name), cancellationToken);

            state.DHCPLease = getDHCPLeaseResponse.DHCPLease;
            state.IpAddresses = [];

            state.UpdateIPv4Mode(networkInterface);

            state.UpdateIPv4Details(networkInterface);

            state.UpdateMacAddress(networkInterface, getOriginalPhysicalAddressResponse.OriginalPhysicalAddress);
        }
        finally
        {
            state.EndLoading();
        }
    }
}
