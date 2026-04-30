using Blazor.Shared.Instance.ControlPanels.Instances.Extensions;
using Sdk.Client.ControlPanels.Services;
using Sdk.Client.Infrastructure;

namespace Blazor.Shared.Instance.ControlPanels.Instances.Services;

public sealed class InstancesControlPanelResetHandler(IUiMediator mediator) : IControlPanelResetHandler<InstancesControlPanelState>
{
    public async Task Reset(InstancesControlPanelState state, CancellationToken cancellationToken)
    {
        state.ResetSelectedInstances = true;
        await state.ReloadInstances(mediator);

        foreach (var instance in state.DeletingInstances.Where(i => i.HasFailed))
            state.Instances.First(i => i.Id == instance.Id).HasFailed = true;

        state.DeletingInstances.Clear();
    }
}
