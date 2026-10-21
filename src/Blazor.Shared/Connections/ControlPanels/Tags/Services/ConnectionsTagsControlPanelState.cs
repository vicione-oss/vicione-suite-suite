using Sdk.Client.ControlPanels.Services;
using Sdk.Connections.Contracts;
using ViciOne.SourceGenerators.NotifyChanged;

namespace Blazor.Shared.Connections.ControlPanels.Tags.Services;

public sealed partial class ConnectionsTagsControlPanelState : ControlPanelState
{
    [NotifyChanged]
    internal partial bool ResetSelectedTags { get; set; }

    [NotifyChanged]
    internal partial Dictionary<Guid, Tag> Tags { get; set; } = [];

    internal List<Tag> DeletingTags { get; } = [];
}
