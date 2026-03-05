using Blazor.Shared.Instance.ControlPanels.Instances.Models;
using Blazor.Shared.Services;
using Core.Shared.Instance.Requests;
using Sdk.Client.ControlPanels.Services;
using Sdk.Client.Infrastructure;

namespace Blazor.Shared.Instance.ControlPanels.Instances.Services;

internal class InstanceControlPanelResetHandler(IUiMediator mediator, IBackendLogService logService) : IControlPanelResetHandler<InstanceControlPanelState>
{
    public async Task Reset(InstanceControlPanelState state, CancellationToken cancellationToken)
    {
        if (state.InstanceId is null)
            return;

        state.BeginLoading();
        try
        {
            var response = await mediator.Request<GetInstances, GetInstancesResponse>(new(state.InstanceId), cancellationToken);
            var instance = response.Instances.FirstOrDefault();
            if (instance is null)
            {
                state.InstanceInformation = null;

                return;
            }

            state.InstanceInformation = new InstanceInformationModel(instance);
            state.LogLevel = await logService.GetLogLevel();
        }
        finally
        {
            state.EndLoading();
        }
    }
}
