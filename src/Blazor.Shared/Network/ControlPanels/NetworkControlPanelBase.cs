using Core.Shared.HostManagement.Services;
using Microsoft.AspNetCore.Components;
using Sdk.Client.ControlPanels.Components;

namespace Blazor.Shared.Network.ControlPanels;

public class NetworkControlPanelBase<TState> : ControlPanelBase<TState>
    where TState : NetworkControlPanelStateBase
{
    [Inject]
    protected ISystemConfigurationService SystemConfigurationService { get; set; } = default!;

    protected override void OnInitialized()
        => SystemConfigurationService.SystemConfigurationChanged += SystemConfigurationChanged;

    protected override ValueTask DisposeAsyncCore()
    {
        SystemConfigurationService.SystemConfigurationChanged -= SystemConfigurationChanged;

        return base.DisposeAsyncCore();
    }

    protected virtual Task SystemConfigurationChanged(CancellationToken cancellationToken)
        => Task.CompletedTask;
}
