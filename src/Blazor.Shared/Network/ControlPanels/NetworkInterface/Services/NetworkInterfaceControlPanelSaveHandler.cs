using System.Globalization;
using System.Net;
using Blazor.Shared.Network.ControlPanels.NetworkInterface.Models;
using Blazor.Shared.Network.Extensions;
using Blazor.Shared.Network.Models;
using Blazor.Shared.Network.Services;
using Blazor.Shared.Settings.NetworkInterface.Enums;
using Core.Shared.HostManagement.Requests;
using Core.Shared.HostManagement.Services;
using HostManagement.Shared.Contracts;
using HostManagement.Shared.Contracts.Network;
using Microsoft.Extensions.Logging;
using Sdk.Client.Infrastructure;
using ViciOne.Ui.Localization.Resources;

namespace Blazor.Shared.Network.ControlPanels.NetworkInterface.Services;

internal sealed class NetworkInterfaceControlPanelSaveHandler(
    IUiMediator mediator,
    ISystemConfigurationService systemConfigurationService,
    ILogger<NetworkInterfaceControlPanelSaveHandler> logger) : NetworkControlPanelSaveHandlerBase<NetworkInterfaceControlPanelState>(mediator, systemConfigurationService, logger)
{
    private readonly IUiMediator _mediator = mediator;

    protected override async Task<ISaveInternalResult> SaveInternal(NetworkInterfaceControlPanelState state)
    {
        var networkInterfaces = SystemConfigurationService.SystemConfiguration.NetworkInterfacesSettings.NetworkInterfaces.ToList();

        var networkInterface = networkInterfaces.ElementAtOrDefault(state.NetworkInterfaceIndex)
            ?? throw new ArgumentOutOfRangeException(nameof(state), string.Format(CultureInfo.CurrentCulture, Localization.NetworkInterfaceControlPanelSaveHandler.CannotFindNetworkInterfaceSettingsBasedOnGivenIndex, state.NetworkInterfaceIndex));

        var newNetworkInterface = new NetworkInterfaceDetail
        {
            CommonInformation = new NetworkInterfaceCommonInformation
            {
                Enabled = networkInterface.CommonInformation.Enabled,
                Name = networkInterface.CommonInformation.Name,
                PhysicalAddress = networkInterface.CommonInformation.PhysicalAddress
            },
            IPv4 = new IPv4Settings()
        };

        var originalPhysicalAddressResponse = await _mediator.Request<GetOriginalPhysicalAddress, GetOriginalPhysicalAddressResponse>(new GetOriginalPhysicalAddress(state.Name));

        newNetworkInterface.CommonInformation.Name = state.Name;
        newNetworkInterface.CommonInformation.Enabled = state.Enabled;
        newNetworkInterface.CommonInformation.PhysicalAddress = string.IsNullOrEmpty(state.MacAddress) ? originalPhysicalAddressResponse.OriginalPhysicalAddress ?? string.Empty : state.MacAddress;

        newNetworkInterface.IPv4.DHCPEnabled = state.IpV4ConfigurationMode == IpConfigurationMode.AutomaticDhcp;

        // filter out fieldsets not filled and remove duplicates
        var ipV4Details = new List<NetworkInterfaceIPv4Detail> { state.FirstIpV4Detail };
        ipV4Details.AddRange(state.AdditionalIpV4Details);

        ipV4Details = [.. ipV4Details.Where(d => !string.IsNullOrWhiteSpace(d.IpAddress) || !string.IsNullOrWhiteSpace(d.SubnetMask)).Distinct()];

        newNetworkInterface.IPv4.IPv4Details.Clear();
        newNetworkInterface.IPv4.IPv4Details.AddRange(
            ipV4Details.Select(d => new IPv4Detail { IPAddress = IPAddress.Parse(d.IpAddress), Netmask = IPAddress.Parse(d.SubnetMask) }));

        ipV4Details.EnsureAtLeastOneItemExists();

        state.FirstIpV4Detail = ipV4Details.First();
        state.AdditionalIpV4Details = [.. ipV4Details.Skip(1)];

        if (string.IsNullOrWhiteSpace(state.DefaultGateway))
            newNetworkInterface.IPv4.Gateway = null;
        else
            newNetworkInterface.IPv4.Gateway = IPAddress.Parse(state.DefaultGateway);

        // ... = _macAddressManually ? _macAddress : string.Empty;

        newNetworkInterface.IPv4.VLANEnabled = state.VLanEnabled;

        if (state.VLanEnabled)
        {
            if (int.TryParse(state.VLanId, out var vLanId))
                newNetworkInterface.IPv4.VLANID = vLanId;
            else
                return new SaveInternalErrorResult(string.Format(CultureInfo.InvariantCulture, ValidationMessages.FieldMustBeAnInteger, Constants.VLanIdLabel));
        }

        networkInterfaces[state.NetworkInterfaceIndex] = newNetworkInterface;

        var systemConfiguration = new SystemConfiguration
        {
            NetworkInterfacesSettings = new NetworkInterfacesSettings
            {
                NetworkInterfaces = networkInterfaces
            },
            NetworkDNSSettings = SystemConfigurationService.SystemConfiguration.NetworkDNSSettings,
            NetworkProxySettings = SystemConfigurationService.SystemConfiguration.NetworkProxySettings,
            NetworkNTPSettings = SystemConfigurationService.SystemConfiguration.NetworkNTPSettings,
            Services = SystemConfigurationService.SystemConfiguration.Services
        };

        return new SystemConfigurationSaveInternalResult(systemConfiguration);
    }
}
