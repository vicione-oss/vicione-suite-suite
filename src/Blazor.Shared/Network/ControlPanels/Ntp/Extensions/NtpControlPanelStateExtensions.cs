using Blazor.Shared.Network.ControlPanels.Ntp.Services;
using Blazor.Shared.Services;
using Core.Shared.HostManagement.Requests;
using Sdk.Client.Infrastructure;

namespace Blazor.Shared.Network.ControlPanels.Ntp.Extensions;

internal static class NtpControlPanelStateExtensions
{
    public static async Task Initialize(this NtpControlPanelState state, ISystemConfigurationService systemConfigurationService, IUiMediator mediator, CancellationToken cancellationToken)
    {
        var networkNtpSettings = systemConfigurationService.SystemConfiguration.NetworkNTPSettings;
        var result = await mediator.Request<GetNTPFallbackInformation, GetNTPFallbackInformationResponse>(new(), cancellationToken);

        state.NtpServersEnabled = networkNtpSettings.NTPServersEnabled;
        state.NtpServerDetails.Reset(networkNtpSettings.NTPServers);
        state.FallbackNtpServerDetails.Reset(result.FallbackNTPServers ?? []);
    }
}
