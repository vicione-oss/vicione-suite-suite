using Blazor.Shared.Network.Models;
using Blazor.Shared.Network.Services;
using Blazor.Shared.Services;
using Core.Shared.HostManagement;
using HostManagement.Shared.Contracts;
using HostManagement.Shared.Contracts.Service;
using HostManagement.Shared.Enums;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sdk.Client.Infrastructure;

namespace Blazor.Shared.Network.ControlPanels.RemoteAccess.Services;

internal sealed class RemoteAccessControlPanelSaveHandler(
    IUiMediator mediator,
    ISystemConfigurationService systemConfigurationService,
    ILogger<RemoteAccessControlPanelSaveHandler> logger,
    IOptions<HostManagementOptions> hostManagementOptions,
    IConfiguration config) : NetworkControlPanelSaveHandlerBase<RemoteAccessControlPanelState>(mediator, systemConfigurationService, logger)
{
    protected override Task<ISaveInternalResult> SaveInternal(RemoteAccessControlPanelState state)
    {
        var services = SystemConfigurationService.SystemConfiguration.Services.ToList();

        SetSshServiceState(state, services);
        SetMoneoRcServiceState(state, services);

        var systemConfiguration = new SystemConfiguration
        {
            NetworkInterfacesSettings = SystemConfigurationService.SystemConfiguration.NetworkInterfacesSettings,
            NetworkDNSSettings = SystemConfigurationService.SystemConfiguration.NetworkDNSSettings,
            NetworkProxySettings = SystemConfigurationService.SystemConfiguration.NetworkProxySettings,
            NetworkNTPSettings = SystemConfigurationService.SystemConfiguration.NetworkNTPSettings,
            Services = services
        };

        return Task.FromResult<ISaveInternalResult>(new SystemConfigurationSaveInternalResult(systemConfiguration));
    }

    private void SetSshServiceState(RemoteAccessControlPanelState state, List<ServiceDetail> services)
    {
        var sshServiceName = hostManagementOptions.Value.SshServiceName;
        ServiceState serviceState;

        if (state.Terminal.CanSecureShell)
            serviceState = state.Terminal.IsSecureShell ? ServiceState.Enabled : ServiceState.Disabled;
        else
            serviceState = ServiceState.Disabled;

        var serviceDetail = services.FirstOrDefault(s => s.Name == sshServiceName);
        if (serviceDetail is null)
            return;

        services.Remove(serviceDetail);
        services.Add(new ServiceDetail { Name = sshServiceName, State = serviceState });
    }

    private void SetMoneoRcServiceState(RemoteAccessControlPanelState state, List<ServiceDetail> services)
    {
        var moneoRcServiceName = config[Constants.MoneoRcServiceNameConfigKey];
        if (string.IsNullOrEmpty(moneoRcServiceName))
            return;

        ServiceState serviceState;

        if (state.Terminal.CanMoneoRc)
            serviceState = state.Terminal.IsMoneoRc ? ServiceState.Enabled : ServiceState.Disabled;
        else
            serviceState = ServiceState.Disabled;

        var serviceDetail = services.FirstOrDefault(s => s.Name == moneoRcServiceName);
        if (serviceDetail is null)
            return;

        services.Remove(serviceDetail);
        services.Add(new ServiceDetail { Name = moneoRcServiceName, State = serviceState });
    }
}
