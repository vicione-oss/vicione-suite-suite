using Blazor.Shared.Network.ControlPanels.Ntp.Extensions;
using Blazor.Shared.Network.Models;
using Blazor.Shared.Network.Services;
using Blazor.Shared.Services;
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
        var networkNtpSettings = new NetworkNTPSettings();

        networkNtpSettings.Enabled = state.NtpServersEnabled;
        state.NtpServerDetails.Save(networkNtpSettings.Servers);

        var systemConfiguration = new SystemConfiguration
        {
            NetworkInterfaces = SystemConfigurationService.SystemConfiguration.NetworkInterfaces,
            NetworkDNSSettings = SystemConfigurationService.SystemConfiguration.NetworkDNSSettings,
            NetworkProxySettings = SystemConfigurationService.SystemConfiguration.NetworkProxySettings,
            NetworkNTPSettings = networkNtpSettings,
            Services = SystemConfigurationService.SystemConfiguration.Services
        };

        return Task.FromResult<ISaveInternalResult>(new SystemConfigurationSaveInternalResult(systemConfiguration));
    }
}
