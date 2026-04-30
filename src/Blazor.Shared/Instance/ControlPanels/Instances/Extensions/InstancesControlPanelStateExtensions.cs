using Blazor.Shared.Instance.ControlPanels.Instances.Models;
using Blazor.Shared.Instance.ControlPanels.Instances.Services;
using Core.Shared.Instance.Requests;
using Sdk.Client.Infrastructure;

namespace Blazor.Shared.Instance.ControlPanels.Instances.Extensions;

internal static class InstancesControlPanelStateExtensions
{
    internal static async Task ReloadInstances(this InstancesControlPanelState state, IUiMediator mediator)
    {
        var response = await mediator.Request<GetInstances, GetInstancesResponse>(new());

        state.Instances = [.. response.Instances.Select(k => new InstanceInformationModel(k))];
    }
}
