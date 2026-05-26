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

        var existingControlPanelRegistryItemMap = controlPanelRegistry.Where(i => i.State is NetworkInterfaceControlPanelState)
            .ToDictionary(k => (k.State as NetworkInterfaceControlPanelState)!.NetworkInterfaceIndex);

        var networkInterfaces = systemConfiguration.NetworkInterfacesSettings.NetworkInterfaces
            .Select((networkInterfaceDetail, networkInterfaceIndex) => new { Detail = networkInterfaceDetail, Index = networkInterfaceIndex })
            .ToList();

        foreach (var networkInterfaceDescriptor in networkInterfaces)
        {
            if (existingControlPanelRegistryItemMap.TryGetValue(networkInterfaceDescriptor.Index, out var existingControlPanelRegistryItem))
            {
                if (existingControlPanelRegistryItem.State is NetworkInterfaceControlPanelState)
                    existingControlPanelRegistryItemMap.Remove(networkInterfaceDescriptor.Index);
            }
            else
            {
                var networkInterfaceControlPanelState = new NetworkInterfaceControlPanelState
                {
                    NetworkInterfaceIndex = networkInterfaceDescriptor.Index
                };

                controlPanelRegistry.Add<NetworkInterfaceControlPanel, NetworkInterfaceControlPanelState>(
                    new NetworkInterfaceControlPanelDescriptor(networkInterfaceControlPanelState, systemConfigurationService),
                    networkInterfaceControlPanelState,
                    new ControlPanelNetworkCategoryDescriptor(),
                    authorizationRequirement: _authorizationRequirement);
            }
        }

        foreach (var existingControlPanelRegistryItem in existingControlPanelRegistryItemMap.Values)
            controlPanelRegistry.Remove(existingControlPanelRegistryItem);
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
