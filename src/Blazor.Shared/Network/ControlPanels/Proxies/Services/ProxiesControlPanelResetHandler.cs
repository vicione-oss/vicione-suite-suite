using Blazor.Shared.Network.ControlPanels.Proxies.Extensions;
using Blazor.Shared.Services;
using Sdk.Client.ControlPanels.Services;

namespace Blazor.Shared.Network.ControlPanels.Proxies.Services;

internal sealed class ProxiesControlPanelResetHandler(ISystemConfigurationService systemConfigurationService) : IControlPanelResetHandler<ProxiesControlPanelState>
{
    public Task Reset(ProxiesControlPanelState state, CancellationToken cancellationToken)
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
