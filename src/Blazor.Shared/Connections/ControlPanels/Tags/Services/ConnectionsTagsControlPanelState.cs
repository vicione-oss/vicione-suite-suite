using Sdk.Client.ControlPanels.Services;
using Sdk.Connections.Contracts;

namespace Blazor.Shared.Connections.ControlPanels.Tags.Services;

public sealed class ConnectionsTagsControlPanelState : ControlPanelState
{
    private bool _resetSelectedTags;
    private Dictionary<Guid, Tag> _tags = [];

    internal bool ResetSelectedTags
    {
        get => _resetSelectedTags;
        set
        {
            if (value == _resetSelectedTags)
                return;

            _resetSelectedTags = value;

            OnPropertyChanged(nameof(ResetSelectedTags));
        }
    }

    internal Dictionary<Guid, Tag> Tags
    {
        get => _tags;
        set
        {
            if (value == _tags)
                return;

            _tags = value;

            OnPropertyChanged(nameof(Tags));
        }
    }

    internal List<Tag> DeletingTags { get; } = [];
}
