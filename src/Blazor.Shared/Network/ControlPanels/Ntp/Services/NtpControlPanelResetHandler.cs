using Blazor.Shared.Network.ControlPanels.Ntp.Extensions;
using Core.Shared.HostManagement.Services;
using Sdk.Client.ControlPanels.Services;

namespace Blazor.Shared.Network.ControlPanels.Ntp.Services;

internal sealed class NtpControlPanelResetHandler(ISystemConfigurationService systemConfigurationService) : IControlPanelResetHandler<NtpControlPanelState>
{
    public Task Reset(NtpControlPanelState state, CancellationToken cancellationToken)
    {
        var networkNtpSettings = systemConfigurationService.SystemConfiguration.NetworkNTPSettings;

        state.NtpServersEnabled = networkNtpSettings.NTPServersEnabled;
        state.NtpServerDetails.Reset(networkNtpSettings.NTPServers);
        state.FallbackNtpServerDetails.Reset(networkNtpSettings.FallbackNTPServers);

        return Task.CompletedTask;
    }
}
