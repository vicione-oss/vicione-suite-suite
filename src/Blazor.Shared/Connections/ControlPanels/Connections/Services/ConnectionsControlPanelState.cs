using System.ComponentModel;
using Blazor.Shared.Connections.Contracts;
using Sdk.Client.ControlPanels.Services;
using Sdk.Client.Infrastructure;
using Sdk.Connections.Contracts;
using Sdk.Connections.Events;
using Sdk.Messaging;

namespace Blazor.Shared.Connections.ControlPanels.Connections.Services;

public sealed class ConnectionsControlPanelState : ControlPanelState, IEventConsumer<TagsChanged>, IDisposable
{
    private bool _resetSelectedConnections;
    private bool _resetSelectedTags;
    private Dictionary<Guid, Tag> _tags = [];
    private readonly IUiMediator _mediator;
    private readonly IDisposable _disposible;

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

    public ConnectionsControlPanelState(IUiMediator mediator)
    {
        _mediator = mediator;

        _disposible = _mediator.Register(this);
    }

    public Task Consume(ClientContext<TagsChanged> context, CancellationToken cancellationToken)
    {
        var tags = new Dictionary<Guid, Tag>(Tags);

        foreach (var tag in context.Message.Tags)
        {
            switch (context.Message.Action)
            {
                case CrudAction.Created:
                case CrudAction.Updated:
                    tags[tag.Id] = tag;
                    break;
                case CrudAction.Deleted: // triggers in SaveHandler
                    break;
                default:
                    throw new InvalidEnumArgumentException(nameof(context.Message.Action), (int)context.Message.Action, typeof(CrudAction));
            }
        }

        Tags = tags;
        return Task.CompletedTask;
    }

    public void Dispose()
        => _disposible.Dispose();
}
