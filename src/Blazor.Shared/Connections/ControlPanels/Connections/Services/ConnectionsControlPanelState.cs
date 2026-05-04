using Blazor.Shared.Connections.Contracts;
using Sdk.Client.ControlPanels.Services;
using Sdk.Connections.Contracts;

namespace Blazor.Shared.Connections.ControlPanels.Connections.Services;

public sealed class ConnectionsControlPanelState : ControlPanelState
{
    private bool _resetSelectedConnections;
    private bool _resetSelectedTags;
    private Dictionary<Guid, Tag> _tags = [];

    internal bool ResetSelectedConnections
    {
        get => _resetSelectedConnections;
        set
        {
            if (value == _resetSelectedConnections)
                return;

            _resetSelectedConnections = value;

            OnPropertyChanged(nameof(ResetSelectedConnections));
        }
    }

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

    internal List<EditConnectionModel> Connections { get; set; } = [];

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

    internal List<EditConnectionModel> DeletingConnections { get; } = [];

    internal List<Tag> DeletingTags { get; } = [];
}
