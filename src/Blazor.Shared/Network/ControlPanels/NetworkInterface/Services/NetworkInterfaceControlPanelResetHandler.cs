using System.Globalization;
using Blazor.Shared.Network.ControlPanels.NetworkInterface.Models;
using Blazor.Shared.Network.Extensions;
using Blazor.Shared.Settings.NetworkInterface.Enums;
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

            List<NetworkInterfaceIPv4Detail> ipV4Details;

            if (networkInterface is not null)
            {
                state.Name = networkInterface.CommonInformation.Name;
                state.Enabled = networkInterface.CommonInformation.Enabled;

                var response = await mediator.Request<GetDHCPLeaseInformation, GetDHCPLeaseInformationResponse>(new GetDHCPLeaseInformation(state.Name), cancellationToken);
                var originalPhysicalAddressResponse = await mediator.Request<GetOriginalPhysicalAddress, GetOriginalPhysicalAddressResponse>(new GetOriginalPhysicalAddress(state.Name), cancellationToken);

                state.DHCPLease = response.DHCPLease;

                if (networkInterface.IPv4.DHCPEnabled && state.DHCPLease is not null)
                {
                    state.IpV4ConfigurationMode = IpConfigurationMode.AutomaticDhcp;
                }
                else if (networkInterface.IPv4.DHCPEnabled && state.DHCPLease is null)
                {
                    state.IpV4ConfigurationMode = IpConfigurationMode.LinkLocal;
                }
                else
                {
                    state.IpV4ConfigurationMode = IpConfigurationMode.Manual;
                }

                ipV4Details = [.. networkInterface.IPv4.IPv4Details
                    .Select(d => new NetworkInterfaceIPv4Detail { IpAddress = d.IPAddress.ToString(), SubnetMask = d.Netmask.ToString() })
                    .Distinct()];

                state.DefaultGateway = networkInterface.IPv4.Gateway?.ToString();

                state.MacAddressManually = networkInterface.CommonInformation.PhysicalAddress != originalPhysicalAddressResponse.OriginalPhysicalAddress;

                if (state.MacAddressManually)
                    state.MacAddress = networkInterface.CommonInformation.PhysicalAddress;
                else
                    state.MacAddress = string.Empty;

                state.VLanEnabled = networkInterface.IPv4.VLANEnabled;
                state.VLanId = networkInterface.IPv4.VLANID.ToString(CultureInfo.InvariantCulture);
            }
            else
            {
                state.Name = string.Empty;
                state.Enabled = false;

                state.IpV4ConfigurationMode = IpConfigurationMode.AutomaticDhcp;
                ipV4Details = [];
                state.DefaultGateway = null;

                state.MacAddressManually = false;
                state.MacAddress = string.Empty;

                state.VLanEnabled = false;
                state.VLanId = string.Empty;
            }

            ipV4Details.EnsureAtLeastOneItemExists();

            state.FirstIpV4Detail = ipV4Details.First();
            state.AdditionalIpV4Details = [.. ipV4Details.Skip(1)];
        }
        finally
        {
            state.EndLoading();
        }
    }
}
