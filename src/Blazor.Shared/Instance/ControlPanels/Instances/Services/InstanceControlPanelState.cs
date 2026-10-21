using Blazor.Shared.Instance.ControlPanels.Instances.Models;
using Microsoft.Extensions.Logging;
using Sdk.Client.ControlPanels.Services;
using ViciOne.SourceGenerators.NotifyChanged;

namespace Blazor.Shared.Instance.ControlPanels.Instances.Services;

public sealed partial class InstanceControlPanelState : ControlPanelState
{
    /// <remarks>
    /// <see cref="InstanceControlPanel"/> will use this property to decide whether an instance could be edited (value is set) or not (value is null)
    /// </remarks>
    [NotifyChanged]
    public partial Guid? InstanceId { get; set; }

    public InstanceInformationModel? InstanceInformation { get; internal set; }
    public LogLevel? LogLevel { get; internal set; }

    public bool CommonSettingsGroupExpanded { get; set; } = true;
    public bool DisplaySettingsGroupExpanded { get; set; } = true;
    public bool StatusSettingsGroupExpanded { get; set; } = true;
}
