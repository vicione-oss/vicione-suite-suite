using Blazor.Shared.Network.ControlPanels.Ntp.Extensions;
using Blazor.Shared.Network.Models;
using Blazor.Shared.Network.Services;
using Core.Shared.HostManagement.Services;
using HostManagement.Shared.Contracts;
using HostManagement.Shared.Contracts.Network;
using Microsoft.Extensions.Logging;
using Sdk.Client.Infrastructure;

namespace Blazor.Shared.Network.ControlPanels.Ntp.Services;

internal sealed class NtpControlPanelSaveHandler(IUiMediator mediator, ISystemConfigurationService systemConfigurationService, ILogger<NtpControlPanelSaveHandler> logger)
    : NetworkControlPanelSaveHandlerBase<NtpControlPanelState>(mediator, systemConfigurationService, logger)
{
    protected override Task<ISaveInternalResult> SaveInternal(NtpControlPanelState state)
    {
        var networkNtpSettings = NetworkNTPSettings.Empty;

        networkNtpSettings.NTPServersEnabled = state.NtpServersEnabled;
        state.NtpServerDetails.Save(networkNtpSettings.NTPServers);

        var systemConfiguration = new SystemConfiguration
        {
            NetworkInterfacesSettings = SystemConfigurationService.SystemConfiguration.NetworkInterfacesSettings,
            NetworkDNSSettings = SystemConfigurationService.SystemConfiguration.NetworkDNSSettings,
            NetworkProxySettings = SystemConfigurationService.SystemConfiguration.NetworkProxySettings,
            NetworkNTPSettings = networkNtpSettings,
            Services = SystemConfigurationService.SystemConfiguration.Services
        };

        return Task.FromResult<ISaveInternalResult>(new SystemConfigurationSaveInternalResult(systemConfiguration));
    }
}
