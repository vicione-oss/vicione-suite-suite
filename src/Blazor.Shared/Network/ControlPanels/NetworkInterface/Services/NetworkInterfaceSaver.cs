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
using Sdk.Client.ControlPanels.Models;
using Sdk.Client.Infrastructure;
using ViciOne.Ui.Localization.Resources;

namespace Blazor.Shared.Network.ControlPanels.NetworkInterface.Services;

/// <summary>
/// Saves the tabs of one network interface with a single <c>SetSystemConfiguration</c>, applying only the tabs that have changes.
/// </summary>
/// <remarks>
/// The settings container saves all running tab edits in parallel. The first tab save handler starts the save and the others
/// join it, because the change flags of every running tab are already set when the first one arrives. Tab save handlers call
/// <see cref="SaveChanges"/>; the inherited <c>Save</c> does not join a running save.
/// </remarks>
internal sealed class NetworkInterfaceSaver(
    IUiMediator mediator,
    ISystemConfigurationService systemConfigurationService,
    ILogger<NetworkInterfaceSaver> logger) : NetworkControlPanelSaveHandlerBase<NetworkInterfaceControlPanelState>(mediator, systemConfigurationService, logger)
{
    private readonly Lock _lock = new();
    private readonly Dictionary<NetworkInterfaceControlPanelState, Task<ISaveResult>> _runningSaves = new(ReferenceEqualityComparer.Instance);

    /// <remarks>
    /// The shared save does not observe <paramref name="cancellationToken"/>, so one tab edit ending does not cancel the save of
    /// the others; the caller only stops waiting for it.
    /// </remarks>
    public Task<ISaveResult> SaveChanges(NetworkInterfaceControlPanelState state, CancellationToken cancellationToken)
    {
        Task<ISaveResult>? save;

        lock (_lock)
        {
            if (!_runningSaves.TryGetValue(state, out save))
            {
                save = SaveAndClearChanges(state);

                if (!save.IsCompleted)
                    _runningSaves[state] = save;
            }
        }

        return save.WaitAsync(cancellationToken);
    }

    private async Task<ISaveResult> SaveAndClearChanges(NetworkInterfaceControlPanelState state)
    {
        try
        {
            var generalHasChanges = state.GeneralHasChanges;
            var tcpIpHasChanges = state.TcpIpHasChanges;
            var hardwareHasChanges = state.HardwareHasChanges;

            // A tab whose changes an earlier save already wrote has nothing left to save.
            if (!generalHasChanges && !tcpIpHasChanges && !hardwareHasChanges)
                return new SaveSuccessResult();

            var result = await Save(state, CancellationToken.None);

            if (result is not SaveErrorResult)
            {
                if (generalHasChanges)
                    state.GeneralHasChanges = false;

                if (tcpIpHasChanges)
                    state.TcpIpHasChanges = false;

                if (hardwareHasChanges)
                    state.HardwareHasChanges = false;
            }

            return result;
        }
        finally
        {
            lock (_lock)
                _runningSaves.Remove(state);
        }
    }

    protected override Task<ISaveInternalResult> SaveInternal(NetworkInterfaceControlPanelState state)
    {
        var networkInterfaces = SystemConfigurationService.SystemConfiguration.NetworkInterfaces.ToList();

        var networkInterface = networkInterfaces.ElementAtOrDefault(state.NetworkInterfaceIndex)
            ?? throw new ArgumentOutOfRangeException(nameof(state), string.Format(CultureInfo.CurrentCulture, Localization.NetworkInterfaceControlPanelSaveHandler.CannotFindNetworkInterfaceSettingsBasedOnGivenIndex, state.NetworkInterfaceIndex));

        var commonInformation = networkInterface.CommonInformation;
        var ipV4 = networkInterface.IPv4;
        var vLan = networkInterface.VLAN;

        if (state.GeneralHasChanges)
            commonInformation = commonInformation with { Enabled = state.Enabled };

        if (state.HardwareHasChanges)
        {
            if (!TryCreateUserDefinedMACAddress(state, out var userDefinedMACAddress))
                return Task.FromResult<ISaveInternalResult>(new SaveInternalErrorResult(string.Format(CultureInfo.CurrentCulture, Localization.NetworkInterfaceControlPanelSaveHandler.MacAddressIsInvalid, state.MacAddress)));

            commonInformation = commonInformation with { UserDefinedMACAddress = userDefinedMACAddress };
        }

        if (state.TcpIpHasChanges)
        {
            ipV4 = CreateIPv4Settings(state);

            if (!TryCreateVLANSettings(state, out var vLanSettings))
                return Task.FromResult<ISaveInternalResult>(new SaveInternalErrorResult(string.Format(CultureInfo.InvariantCulture, ValidationMessages.FieldMustBeAnInteger, Constants.VLanIdLabel)));

            vLan = vLanSettings;
        }

        networkInterfaces[state.NetworkInterfaceIndex] = networkInterface with { CommonInformation = commonInformation, IPv4 = ipV4, VLAN = vLan };

        var systemConfiguration = new SystemConfiguration
        {
            NetworkInterfaces = networkInterfaces,
            NetworkDNSSettings = SystemConfigurationService.SystemConfiguration.NetworkDNSSettings,
            NetworkProxySettings = SystemConfigurationService.SystemConfiguration.NetworkProxySettings,
            NetworkNTPSettings = SystemConfigurationService.SystemConfiguration.NetworkNTPSettings,
            Services = SystemConfigurationService.SystemConfiguration.Services
        };

        return Task.FromResult<ISaveInternalResult>(new SystemConfigurationSaveInternalResult(systemConfiguration));
    }

    private static IPv4Settings CreateIPv4Settings(NetworkInterfaceControlPanelState state)
    {
        var ipV4 = new IPv4Settings { DHCPEnabled = state.IpV4ConfigurationMode == IpConfigurationMode.AutomaticDhcp };

        // Unfilled fieldsets and duplicates are dropped.
        var ipV4Details = new List<NetworkInterfaceIPv4Detail> { state.FirstIpV4Detail };
        ipV4Details.AddRange(state.AdditionalIpV4Details);

        ipV4Details = [.. ipV4Details.Where(d => !string.IsNullOrWhiteSpace(d.IpAddress) || !string.IsNullOrWhiteSpace(d.SubnetMask)).Distinct()];

        ipV4.IPv4Details.AddRange(
            ipV4Details.Select(d => new IPv4Detail { IPAddress = IPAddress.Parse(d.IpAddress), Netmask = IPAddress.Parse(d.SubnetMask) }));

        ipV4Details.EnsureAtLeastOneItemExists();

        state.FirstIpV4Detail = ipV4Details.First();
        state.AdditionalIpV4Details = [.. ipV4Details.Skip(1)];

        ipV4.Gateway = string.IsNullOrWhiteSpace(state.DefaultGateway) ? null : IPAddress.Parse(state.DefaultGateway);

        return ipV4;
    }

    private static bool TryCreateVLANSettings(NetworkInterfaceControlPanelState state, [NotNullWhen(true)] out VLANSettings? vLanSettings)
    {
        vLanSettings = new VLANSettings { Enabled = state.VLanEnabled };

        if (!state.VLanEnabled)
            return true;

        if (int.TryParse(state.VLanId, out var vLanId))
        {
            vLanSettings.ID = vLanId;
            return true;
        }

        vLanSettings = null;
        return false;
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
