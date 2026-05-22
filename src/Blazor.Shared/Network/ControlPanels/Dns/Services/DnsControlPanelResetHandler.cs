using Blazor.Shared.Network.ControlPanels.Dns.Extensions;
using Blazor.Shared.Services;
using Sdk.Client.ControlPanels.Services;

namespace Blazor.Shared.Network.ControlPanels.Dns.Services;

internal sealed class DnsControlPanelResetHandler(ISystemConfigurationService systemConfigurationService) : IControlPanelResetHandler<DnsControlPanelState>
{
    public Task Reset(DnsControlPanelState state, CancellationToken cancellationToken)
    {
        state.BeginLoading();
        try
        {
            state.Initialize(systemConfigurationService);
        }
        finally
        {
            state.EndLoading();
        }

        return Task.CompletedTask;
    }
}
