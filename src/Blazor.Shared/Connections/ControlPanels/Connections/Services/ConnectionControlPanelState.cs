using Blazor.Shared.Connections.Contracts;
using Sdk.Client.ControlPanels.Services;
using Sdk.Connections.Contracts;

namespace Blazor.Shared.Connections.ControlPanels.Connections.Services;

public sealed class ConnectionControlPanelState : ControlPanelState
{
    private Guid? _connectionId;

    /// <remarks>
    /// This property is used to decide whether a connection could be edited (value is set) or not (value is null)
    /// </remarks>
    internal Guid? ConnectionId
    {
        get => _connectionId;
        set
        {
            if (value != _connectionId)
            {
                _connectionId = value;

                OnPropertyChanged();
            }
        }
    }

    internal bool CommonSettingsGroupExpanded { get; set; } = true;
    internal bool DisplaySettingsGroupExpanded { get; set; } = true;
    internal bool StatusSettingsGroupExpanded { get; set; } = true;

    internal EditConnectionModel? EditConnectionModel { get; set; }

    internal IEnumerable<string> EditModelTagTexts { get; set; } = [];

    internal List<string> AvailableTagTexts { get; set; } = [];

    internal List<Tag> AvailableTags { get; set; } = [];
}
