using Blazor.Shared.Instance.ControlPanels.Instances.Models;
using Core.Shared.Instance.Requests;
using Sdk.Client.ControlPanels.Services;
using Sdk.Client.Infrastructure;

namespace Blazor.Shared.Instance.ControlPanels.Instances.Services;

public sealed class InstancesControlPanelState(IUiMediator mediator) : ControlPanelState
{
    private bool _resetSelectedInstances;
    private List<InstanceInformationModel> _instances = [];

    internal bool ResetSelectedInstances
    {
        get => _resetSelectedInstances;
        set
        {
            if (value == _resetSelectedInstances)
                return;

            _resetSelectedInstances = value;

            OnPropertyChanged(nameof(ResetSelectedInstances));
        }
    }

    internal List<InstanceInformationModel> Instances
    {
        get => _instances;
        set
        {
            if (value == _instances)
                return;

            _instances = value;

            OnPropertyChanged(nameof(Instances));
        }
    }

    internal List<InstanceInformationModel> DeletingInstances { get; } = [];

    internal async Task ReloadInstances()
    {
        var response = await mediator.Request<GetInstances, GetInstancesResponse>(new());

        Instances = [.. response.Instances.Select(k => new InstanceInformationModel(k))];
    }
}
