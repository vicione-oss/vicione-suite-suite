using Blazor.Shared.Connections.Contracts;
using Sdk.Client.ControlPanels.Services;
using Sdk.Connections.Contracts;
using ViciOne.SourceGenerators.NotifyChanged;

namespace Blazor.Shared.Connections.ControlPanels.Connections.Services;

public sealed partial class ConnectionControlPanelState : ControlPanelState
{
    /// <remarks>
    /// This property is used to decide whether a connection could be edited (value is set) or not (value is null)
    /// </remarks>
    [NotifyChanged]
    internal partial Guid? ConnectionId { get; set; }

    internal bool CommonSettingsGroupExpanded { get; set; } = true;
    internal bool DisplaySettingsGroupExpanded { get; set; } = true;
    internal bool StatusSettingsGroupExpanded { get; set; } = true;

    internal EditConnectionModel? EditConnectionModel { get; set; }

    internal IEnumerable<string> EditModelTagTexts { get; set; } = [];

    internal List<string> AvailableTagTexts { get; set; } = [];

    internal List<Tag> AvailableTags { get; set; } = [];
}
