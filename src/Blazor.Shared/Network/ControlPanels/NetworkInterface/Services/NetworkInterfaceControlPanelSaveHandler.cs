using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Net;
using System.Net.NetworkInformation;
using Blazor.Shared.Network.ControlPanels.NetworkInterface.Models;
using Blazor.Shared.Network.Extensions;
using Blazor.Shared.Network.Models;
using Blazor.Shared.Network.Services;
using Blazor.Shared.Services;
using Blazor.Shared.Settings.NetworkInterface.Enums;
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
    protected override Task<ISaveInternalResult> SaveInternal(NetworkInterfaceControlPanelState state)
    {
        var networkInterfaces = SystemConfigurationService.SystemConfiguration.NetworkInterfaces.ToList();

        var networkInterface = networkInterfaces.ElementAtOrDefault(state.NetworkInterfaceIndex)
            ?? throw new ArgumentOutOfRangeException(nameof(state), string.Format(CultureInfo.CurrentCulture, Localization.NetworkInterfaceControlPanelSaveHandler.CannotFindNetworkInterfaceSettingsBasedOnGivenIndex, state.NetworkInterfaceIndex));

        var newNetworkInterface = new NetworkInterfaceDetail
        {
            CommonInformation = new NetworkInterfaceCommonInformation
            {
                Enabled = networkInterface.CommonInformation.Enabled,
                Name = networkInterface.CommonInformation.Name,
                UserDefinedMACAddress = networkInterface.CommonInformation.UserDefinedMACAddress
            },
            IPv4 = new IPv4Settings()
        };

        newNetworkInterface.CommonInformation.Name = state.Name;
        newNetworkInterface.CommonInformation.Enabled = state.Enabled;

        if (!TryCreateUserDefinedMACAddress(state, out var userDefinedMACAddress))
            return Task.FromResult<ISaveInternalResult>(new SaveInternalErrorResult(string.Format(CultureInfo.CurrentCulture, Localization.NetworkInterfaceControlPanelSaveHandler.MacAddressIsInvalid, state.MacAddress)));

        newNetworkInterface.CommonInformation.UserDefinedMACAddress = userDefinedMACAddress;

        newNetworkInterface.IPv4.DHCPEnabled = state.IpV4ConfigurationMode == IpConfigurationMode.AutomaticDhcp;

        // Unfilled fieldsets and duplicates are dropped.
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

        newNetworkInterface.VLAN.Enabled = state.VLanEnabled;

        if (state.VLanEnabled)
        {
            if (int.TryParse(state.VLanId, out var vLanId))
                newNetworkInterface.VLAN.ID = vLanId;
            else
                return Task.FromResult<ISaveInternalResult>(new SaveInternalErrorResult(string.Format(CultureInfo.InvariantCulture, ValidationMessages.FieldMustBeAnInteger, Constants.VLanIdLabel)));
        }

        networkInterfaces[state.NetworkInterfaceIndex] = newNetworkInterface;

        var systemConfiguration = new SystemConfiguration
        {
            NetworkInterfaces = networkInterfaces,
            NetworkDNSSettings = SystemConfigurationService.SystemConfiguration.NetworkDNSSettings,
            NetworkProxySettings = SystemConfigurationService.SystemConfiguration.NetworkProxySettings,
            NetworkNTPSettings = SystemConfigurationService.SystemConfiguration.NetworkNTPSettings,
            Services = SystemConfigurationService.SystemConfiguration.Services
        };

        state.HasUnsavedChanges = false;

        return Task.FromResult<ISaveInternalResult>(new SystemConfigurationSaveInternalResult(systemConfiguration));
    }

    [SuppressMessage("Interoperability", "CA1416:Validate platform compatibility", Justification = "Blazor.Shared is only rendered on the server.")]
    private static bool TryCreateUserDefinedMACAddress(NetworkInterfaceControlPanelState state, [NotNullWhen(true)] out UserDefinedMACAddressSettings? userDefinedMACAddress)
    {
        if (!state.MacAddressManually)
        {
            userDefinedMACAddress = new UserDefinedMACAddressSettings();
            return true;
        }

        if (PhysicalAddress.TryParse(state.MacAddress, out var address))
        {
            userDefinedMACAddress = new UserDefinedMACAddressSettings { Enabled = true, Address = address };
            return true;
        }

        userDefinedMACAddress = null;
        return false;
    }
}
