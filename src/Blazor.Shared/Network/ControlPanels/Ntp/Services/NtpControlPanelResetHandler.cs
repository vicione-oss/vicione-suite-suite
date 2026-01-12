using Blazor.Shared.Network.ControlPanels.Ntp.Extensions;
using Core.Shared.HostManagement.Requests;
using Core.Shared.HostManagement.Services;
using Sdk.Client.ControlPanels.Services;
using Sdk.Client.Infrastructure;

namespace Blazor.Shared.Network.ControlPanels.Ntp.Services;

internal sealed class NtpControlPanelResetHandler(ISystemConfigurationService systemConfigurationService, IUiMediator mediator) : IControlPanelResetHandler<NtpControlPanelState>
{
    public async Task Reset(NtpControlPanelState state, CancellationToken cancellationToken)
    {
        var networkNtpSettings = systemConfigurationService.SystemConfiguration.NetworkNTPSettings;
        var result = await mediator.Request<GetNTPFallbackInformation, GetNTPFallbackInformationResponse>(new(), cancellationToken);

        state.NtpServersEnabled = networkNtpSettings.NTPServersEnabled;
        state.NtpServerDetails.Reset(networkNtpSettings.NTPServers);
        state.FallbackNtpServerDetails.Reset(result.FallbackNTPServers ?? []);
    }
}
