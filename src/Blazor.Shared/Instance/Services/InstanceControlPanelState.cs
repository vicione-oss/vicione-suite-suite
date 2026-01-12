using Blazor.Shared.Instance.Contracts;
using Blazor.Shared.Instance.ControlPanels;
using Microsoft.Extensions.Logging;
using Sdk.Client.ControlPanels.Services;

namespace Blazor.Shared.Instance.Services;

public sealed class InstanceControlPanelState : ControlPanelState
{
    private Guid? _instanceId;

    /// <remarks>
    /// <see cref="InstanceControlPanel"/> will use this property to decide whether an instance could be edited (value is set) or not (value is null)
    /// </remarks>
    public Guid? InstanceId
    {
        get => _instanceId;
        set
        {
            if (value != _instanceId)
            {
                _instanceId = value;

                OnPropertyChanged();
            }
        }
    }

    public InstanceInformationModel? InstanceInformation { get; internal set; }
    public LogLevel? LogLevel { get; internal set; }

    public bool CommonSettingsGroupExpanded { get; set; } = true;
    public bool DisplaySettingsGroupExpanded { get; set; } = true;
    public bool StatusSettingsGroupExpanded { get; set; } = true;
}
