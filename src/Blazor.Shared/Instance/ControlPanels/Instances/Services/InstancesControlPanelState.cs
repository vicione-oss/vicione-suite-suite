using Blazor.Shared.Instance.ControlPanels.Instances.Models;
using Sdk.Client.ControlPanels.Services;
using ViciOne.SourceGenerators.NotifyChanged;

namespace Blazor.Shared.Instance.ControlPanels.Instances.Services;

public sealed partial class InstancesControlPanelState : ControlPanelState
{
    [NotifyChanged]
    internal partial bool ResetSelectedInstances { get; set; }

    [NotifyChanged]
    internal partial List<InstanceInformationModel> Instances { get; set; } = [];

    internal List<InstanceInformationModel> DeletingInstances { get; } = [];
}
