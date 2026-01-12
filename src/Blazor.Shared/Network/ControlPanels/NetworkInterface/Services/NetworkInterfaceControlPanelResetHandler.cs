using System.Globalization;
using Blazor.Shared.Network.ControlPanels.NetworkInterface.Models;
using Blazor.Shared.Network.Extensions;
using Blazor.Shared.Settings.NetworkInterface.Enums;
using Core.Shared.HostManagement.Services;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Client.ControlPanels.Services;

namespace Blazor.Shared.Network.ControlPanels.NetworkInterface.Services;

internal sealed class NetworkInterfaceControlPanelResetHandler([FromKeyedServices(Sdk.Constants.ClientTimeProviderServiceKey)] TimeProvider timeProvider, ISystemConfigurationService systemConfigurationService)
    : IControlPanelResetHandler<NetworkInterfaceControlPanelState>
{
    public Task Reset(NetworkInterfaceControlPanelState state, CancellationToken cancellationToken)
    {
        var networkInterface = systemConfigurationService.SystemConfiguration.NetworkInterfacesSettings.NetworkInterfaces
            .ElementAtOrDefault(state.NetworkInterfaceIndex);

        List<NetworkInterfaceIPv4Detail> ipV4Details;

        if (networkInterface is not null)
        {
            state.Name = networkInterface.CommonInformation.Name;
            state.Enabled = networkInterface.CommonInformation.Enabled;

            if (networkInterface.IPv4.DHCPEnabled)
                state.IpV4ConfigurationMode = IpConfigurationMode.AutomaticDhcp;
            else
                state.IpV4ConfigurationMode = IpConfigurationMode.Manual;

            ipV4Details = [.. networkInterface.IPv4.IPv4Details
                    .Select(d => new NetworkInterfaceIPv4Detail { IpAddress = d.IPAddress.ToString(), SubnetMask = d.Netmask.ToString() })
                    .Distinct()];

            state.DefaultGateway = networkInterface.IPv4.Gateway?.ToString();

            state.MacAddress = string.Empty; // no property available to fetch the manually set MAC address from
            state.MacAddressManually = !string.IsNullOrWhiteSpace(state.MacAddress);

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

        var timeZone = timeProvider.LocalTimeZone;

        return Task.CompletedTask;
    }
}
