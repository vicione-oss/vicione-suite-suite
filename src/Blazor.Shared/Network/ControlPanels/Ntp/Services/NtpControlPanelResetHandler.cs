using Blazor.Shared.Network.ControlPanels.Ntp.Extensions;
using Blazor.Shared.Services;
using Sdk.Client.ControlPanels.Services;
using Sdk.Client.Infrastructure;

namespace Blazor.Shared.Network.ControlPanels.Ntp.Services;

internal sealed class NtpControlPanelResetHandler(ISystemConfigurationService systemConfigurationService, IUiMediator mediator) : IControlPanelResetHandler<NtpControlPanelState>
{
    public async Task Reset(NtpControlPanelState state, CancellationToken cancellationToken)
    {
        state.BeginLoading();
        try
        {
            await state.Initialize(systemConfigurationService, mediator, cancellationToken);
        }
        finally
        {
            state.EndLoading();
        }
    }
}
