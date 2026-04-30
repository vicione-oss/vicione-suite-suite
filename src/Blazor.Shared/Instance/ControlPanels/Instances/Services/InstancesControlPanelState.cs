using Blazor.Shared.Instance.ControlPanels.Instances.Models;
using Sdk.Client.ControlPanels.Services;

namespace Blazor.Shared.Instance.ControlPanels.Instances.Services;

public sealed class InstancesControlPanelState : ControlPanelState
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
}
