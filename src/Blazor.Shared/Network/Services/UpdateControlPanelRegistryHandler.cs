using Blazor.Shared.Network.ControlPanels;
using Blazor.Shared.Network.ControlPanels.Dns.Components;
using Blazor.Shared.Network.ControlPanels.Dns.Services;
using Blazor.Shared.Network.ControlPanels.NetworkInterface.Components;
using Blazor.Shared.Network.ControlPanels.NetworkInterface.Services;
using Blazor.Shared.Network.ControlPanels.Ntp.Components;
using Blazor.Shared.Network.ControlPanels.Ntp.Services;
using Blazor.Shared.Network.ControlPanels.Proxies.Components;
using Blazor.Shared.Network.ControlPanels.Proxies.Services;
using Blazor.Shared.Network.ControlPanels.RemoteAccess.Components;
using Blazor.Shared.Network.ControlPanels.RemoteAccess.Services;
using Blazor.Shared.Services;
using HostManagement.Shared.Contracts;
using Sdk.Authorization;
using Sdk.Client.ControlPanels.Services;

namespace Blazor.Shared.Network.Services;

internal sealed class UpdateControlPanelRegistryHandler(IControlPanelRegistry<SharedClientModule> controlPanelRegistry, ISystemConfigurationService systemConfigurationService)
    : IUpdateControlPanelRegistryHandler
{
    private readonly AccessLevelAuthorizationRequirement _authorizationRequirement = new(SharedClientModule.ModuleId, AccessLevel.Full);

    public async Task Execute(CancellationToken cancellationToken)
    {
        controlPanelRegistry.BeginUpdate();
        try
        {
            var systemConfiguration = systemConfigurationService.SystemConfiguration;

            UpdateNetworkInterfaceControlPanelRegistrations(systemConfiguration);
            UpdateControlPanelRegistration<ProxiesControlPanel, ProxiesControlPanelState, ProxiesControlPanelDescriptor>(systemConfiguration);
            UpdateControlPanelRegistration<DnsControlPanel, DnsControlPanelState, DnsControlPanelDescriptor>(systemConfiguration);
            UpdateControlPanelRegistration<NtpControlPanel, NtpControlPanelState, NtpControlPanelDescriptor>(systemConfiguration);
            UpdateControlPanelRegistration<RemoteAccessControlPanel, RemoteAccessControlPanelState, RemoteAccessControlPanelDescriptor>(systemConfiguration);
        }
        finally
        {
            controlPanelRegistry.EndUpdate();
        }
    }

    private void UpdateNetworkInterfaceControlPanelRegistrations(SystemConfiguration? systemConfiguration)
    {
        if (systemConfiguration is null)
        {
            controlPanelRegistry.Remove(i => i.State is NetworkInterfaceControlPanelState);

            return;
        }

        // Each interface registers a parent without content of its own plus three children (General, TcpIp, Hardware),
        // all sharing its NetworkInterfaceControlPanelState instance, so - unlike a single-item-per-index map - a lookup
        // here must tolerate multiple registry items per NetworkInterfaceIndex.
        var existingItemsByIndex = controlPanelRegistry.Where(i => i.State is NetworkInterfaceControlPanelState)
            .ToLookup(k => (k.State as NetworkInterfaceControlPanelState)!.NetworkInterfaceIndex);

        var networkInterfaces = systemConfiguration.NetworkInterfaces
            .Select((networkInterfaceDetail, networkInterfaceIndex) => new { Detail = networkInterfaceDetail, Index = networkInterfaceIndex })
            .ToList();

        var seenIndexes = new HashSet<int>();

        foreach (var networkInterfaceDescriptor in networkInterfaces)
        {
            seenIndexes.Add(networkInterfaceDescriptor.Index);

            if (existingItemsByIndex.Contains(networkInterfaceDescriptor.Index))
                continue;

            var networkInterfaceControlPanelState = new NetworkInterfaceControlPanelState
            {
                NetworkInterfaceIndex = networkInterfaceDescriptor.Index
            };

            var categoryDescriptor = new ControlPanelNetworkCategoryDescriptor();
            var parentPanelDescriptor = new NetworkInterfaceControlPanelDescriptor(networkInterfaceControlPanelState, systemConfigurationService);

            controlPanelRegistry.Add<NetworkInterfaceControlPanel, NetworkInterfaceControlPanelState>(
                parentPanelDescriptor,
                networkInterfaceControlPanelState,
                categoryDescriptor,
                authorizationRequirement: _authorizationRequirement,
                asParent: parentPanelDescriptor);

            controlPanelRegistry.Add<NetworkInterfaceGeneralControlPanel, NetworkInterfaceControlPanelState>(
                new NetworkInterfaceGeneralControlPanelDescriptor(),
                networkInterfaceControlPanelState,
                categoryDescriptor,
                authorizationRequirement: _authorizationRequirement,
                childOf: parentPanelDescriptor);

            controlPanelRegistry.Add<NetworkInterfaceTcpIpControlPanel, NetworkInterfaceControlPanelState>(
                new NetworkInterfaceTcpIpControlPanelDescriptor(),
                networkInterfaceControlPanelState,
                categoryDescriptor,
                authorizationRequirement: _authorizationRequirement,
                childOf: parentPanelDescriptor);

            controlPanelRegistry.Add<NetworkInterfaceHardwareControlPanel, NetworkInterfaceControlPanelState>(
                new NetworkInterfaceHardwareControlPanelDescriptor(),
                networkInterfaceControlPanelState,
                categoryDescriptor,
                authorizationRequirement: _authorizationRequirement,
                childOf: parentPanelDescriptor);
        }

        var staleItems = existingItemsByIndex
            .Where(g => !seenIndexes.Contains(g.Key))
            .SelectMany(g => g)
            .ToList();

        foreach (var staleItem in staleItems)
            controlPanelRegistry.Remove(staleItem);
    }

    private void UpdateControlPanelRegistration<TComponent, TState, TDescriptor>(SystemConfiguration? systemConfiguration)
        where TComponent : NetworkControlPanelBase<TState>
        where TState : NetworkControlPanelStateBase, new()
        where TDescriptor : IControlPanelDescriptor<TComponent>, new()
    {
        if (systemConfiguration is null)
        {
            controlPanelRegistry.Remove(i => i.State is TState);

            return;
        }

        var state = controlPanelRegistry.Select(i => i.State).OfType<TState>().FirstOrDefault();
        if (state is null)
        {
            controlPanelRegistry.Add<TComponent, TState>(
                new TDescriptor(),
                new TState(),
                new ControlPanelNetworkCategoryDescriptor(),
                authorizationRequirement: _authorizationRequirement
            );
        }
    }
}
