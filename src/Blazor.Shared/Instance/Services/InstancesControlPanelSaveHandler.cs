using Core.Shared.Instance.Commands;
using Sdk.Client.ControlPanels.Models;
using Sdk.Client.ControlPanels.Services;
using Sdk.Client.Infrastructure;

namespace Blazor.Shared.Instance.Services;

public sealed class InstancesControlPanelSaveHandler(IUiMediator mediator) : IControlPanelSaveHandler<InstancesControlPanelState>
{
    public async Task<ISaveResult> Save(InstancesControlPanelState state, CancellationToken cancellationToken)
    {
        try
        {
            foreach (var deletingInstance in state.DeletingInstances.ToList())
            {
                await mediator.Send(new ControlInstance
                {
                    Action = InstanceCommand.Delete,
                    InstanceId = deletingInstance.Id
                }, deletingInstance.Id, cancellationToken);
            }
        }
        catch (Exception e)
        {
            return new SaveErrorResult(e.Message);
        }

        return new SaveSuccessResult();
    }
}

