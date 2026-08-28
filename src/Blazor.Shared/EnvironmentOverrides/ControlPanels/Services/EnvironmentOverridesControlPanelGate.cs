using Blazor.Shared.EnvironmentOverrides.ControlPanels.Components;
using Core.Shared.EnvironmentOverrides;
using Sdk.Client.ControlPanels.Services;

namespace Blazor.Shared.EnvironmentOverrides.ControlPanels.Services;

/// <summary>
/// Withdraws the panel on an instance that does not apply overrides. It reads the same switch the
/// startup loader did, in the same process, so the panel is offered exactly where a save would
/// have an effect.
/// </summary>
internal sealed class EnvironmentOverridesControlPanelGate(
    IControlPanelRegistry<SharedClientModule> controlPanelRegistry)
    : IUpdateControlPanelRegistryHandler
{
    public Task Execute(CancellationToken cancellationToken)
    {
        if (!EnvironmentOverridesSwitch.IsEnabled())
            controlPanelRegistry.Remove<EnvironmentOverridesControlPanel>();

        return Task.CompletedTask;
    }
}
