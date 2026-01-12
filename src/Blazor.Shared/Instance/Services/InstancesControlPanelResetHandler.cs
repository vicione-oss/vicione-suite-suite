using Sdk.Client.ControlPanels.Services;

namespace Blazor.Shared.Instance.Services;

public sealed class InstancesControlPanelResetHandler : IControlPanelResetHandler<InstancesControlPanelState>
{
    public async Task Reset(InstancesControlPanelState state, CancellationToken cancellationToken)
    {
        state.ResetSelectedInstances = true;
        await state.ReloadInstances();

        foreach (var instance in state.DeletingInstances.Where(i => i.HasFailed))
            state.Instances.First(i => i.Id == instance.Id).HasFailed = true;

        state.DeletingInstances.Clear();
    }
}
