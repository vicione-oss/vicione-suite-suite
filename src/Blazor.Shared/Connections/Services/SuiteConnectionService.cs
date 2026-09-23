using Blazor.Shared.Connections.Contracts;
using Sdk.Client.ControlPanels.Services;
using Sdk.Client.Infrastructure;
using Sdk.Connections.Commands;
using Sdk.Connections.Contracts;
using Sdk.Connections.Events;
using Sdk.Connections.Extensions;
using Sdk.Connections.Requests;
using Sdk.Messaging;

namespace Blazor.Shared.Connections.Services;

internal sealed class SuiteConnectionService : CompletionSourceHandlerBase<ISuiteConnectionServiceResult>,
    ISuiteConnectionService,
    IEventConsumer<ConnectionChanged>,
    IEventConsumer<TagsChanged>
{
    private bool _initialized;
    private List<Connection> _connections = [];
    private readonly HashSet<Tag> _cachedTags = [];
    public IReadOnlySet<Tag> CachedTags => _cachedTags;
    public IReadOnlyList<Connection> Connections => _connections;

    public event Func<IReadOnlyList<Connection>, Task>? ConnectionStateChanged;

    public event Func<ConnectionChanged, Task>? ConnectionChanged;

    public event Func<TagsChanged, Task>? TagsChanged;

    public SuiteConnectionService(IUiMediator mediator) : base(mediator)
    {
        Register<ConnectionChanged>();
        Register<TagsChanged>();
    }

    public async Task Initialize(CancellationToken cancellationToken = default)
    {
        if (!_initialized)
        {
            _connections = await GetConnections(null, cancellationToken);
            foreach (var tag in await GetTags(cancellationToken))
                _cachedTags.Add(tag);

            _initialized = true;
        }

        await NotifyConnectionStateChanged();
    }

    public async Task<Connection?> GetConnection(Guid connectionId, CancellationToken cancellationToken = default)
    {
        var response = await Mediator.Request<GetConnections, GetConnectionsResponse>(new(connectionId, null), cancellationToken);

        return response.Connections.FirstOrDefault();
    }

    public async Task<List<Connection>> GetConnections(List<string>? filterTypes, CancellationToken cancellationToken = default)
    {
        var response = await Mediator.Request<GetConnections, GetConnectionsResponse>(new(null, filterTypes), cancellationToken);

        return response.Connections;
    }

    public async Task<Tag?> GetTag(Guid tagId, CancellationToken cancellationToken = default)
    {
        var tags = await GetTags(cancellationToken);

        return tags.FirstOrDefault(t => t.Id == tagId);
    }

    public async Task<List<Tag>> GetTags(CancellationToken cancellationToken = default)
    {
        var response = await Mediator.Request<GetTags, GetTagsResponse>(new(), cancellationToken);

        return response.Tags;
    }

    public async Task<ISuiteConnectionServiceResult> UpsertConnection(Connection connection, CancellationToken cancellationToken = default)
    {
        var command = new UpsertConnection(connection);

        return await SendAndWaitForCompletion(command, cancellationToken);
    }

    public async Task<ISuiteConnectionServiceResult> DeleteConnection(Connection connection, CancellationToken cancellationToken = default)
    {
        var command = new DeleteConnection(connection.Id);

        return await SendAndWaitForCompletion(command, cancellationToken);
    }

    public async Task Consume(ClientContext<ConnectionChanged> context, CancellationToken cancellationToken = default)
    {
        if (!_initialized)
            return;

        if (context.Message.ErrorInfo is not null)
        {
            CompleteWithError(context.Message.CorrelationId, context.Message.ErrorInfo);

            await NotifyConnectionChanged(context.Message);
            return;
        }

        // Signals the awaiting command that the event was processed, so it can complete.
        CompleteWithSuccess(context.Message.CorrelationId);

        if (ApplyConnectionChange(context.Message))
            await NotifyConnectionStateChanged();

        await NotifyConnectionChanged(context.Message);
    }

    private bool ApplyConnectionChange(ConnectionChanged message)
    {
        var somethingChanged = false;

        switch (message.Action)
        {
            case CrudAction.Created:
                foreach (var tag in message.AddedTags)
                    somethingChanged |= _cachedTags.Add(tag);

                if (!_connections.Any(c => c.Id == message.Connection.Id))
                {
                    _connections.Add(message.Connection);

                    somethingChanged = true;
                }

                break;

            case CrudAction.Updated:
                foreach (var tag in message.AddedTags)
                    somethingChanged |= _cachedTags.Add(tag);

                var existingConnection = _connections.FirstOrDefault(c => c.Id == message.Connection.Id);
                if (existingConnection is not null)
                {
                    existingConnection.Assign(message.Connection);

                    somethingChanged = true;
                }

                break;

            case CrudAction.Deleted:
                somethingChanged = _connections.RemoveAll(c => c.Id == message.Connection.Id) > 0;

                break;
        }

        return somethingChanged;
    }

    private async Task NotifyConnectionStateChanged()
    {
        if (ConnectionStateChanged is not null)
            await ConnectionStateChanged.Invoke(_connections);
    }

    private async Task NotifyConnectionChanged(ConnectionChanged changeEvent)
    {
        if (ConnectionChanged is not null)
            await ConnectionChanged.Invoke(changeEvent);
    }

    private async Task NotifyTagsChanged(TagsChanged changeEvent)
    {
        if (TagsChanged is not null)
            await TagsChanged.Invoke(changeEvent);
    }

    public async Task<ISuiteConnectionServiceResult> UpsertTag(Tag tag, CancellationToken cancellationToken = default)
    {
        var command = new UpsertTag(tag);

        return await SendAndWaitForCompletion(command, cancellationToken);
    }

    public async Task<ISuiteConnectionServiceResult> DeleteTag(Tag tag, CancellationToken cancellationToken = default)
    {
        if (tag.Protected)
            return new SuiteConnectionServiceErrorResult(Localization.SuiteConnectionService.TagCannotBeDeletedBecauseItIsProtected);

        var command = new DeleteTag(tag.Id);

        return await SendAndWaitForCompletion(command, cancellationToken);
    }

    public async Task Consume(ClientContext<TagsChanged> context, CancellationToken cancellationToken = default)
    {
        if (!_initialized)
            return;

        if (context.Message.ErrorInfo is not null)
        {
            CompleteWithError(context.Message.CorrelationId, context.Message.ErrorInfo);
            await NotifyTagsChanged(context.Message);
            return;
        }

        CompleteWithSuccess(context.Message.CorrelationId);

        if (ApplyTagChange(context.Message))
            await NotifyConnectionStateChanged();

        await NotifyTagsChanged(context.Message);
    }

    private bool ApplyTagChange(TagsChanged message)
    {
        var connectionChanged = false;
        switch (message.Action)
        {
            case CrudAction.Created:
                foreach (var tag in message.Tags)
                    _cachedTags.Add(tag);

                break;

            case CrudAction.Updated:
                foreach (var tag in message.Tags)
                {
                    if (_cachedTags.TryGetValue(tag, out var existing))
                        existing.Text = tag.Text;

                    foreach (var connection in _connections.Where(connection => connection.Tags.Contains(tag)))
                    {
                        connection.Tags.First(t => t.Id == tag.Id).Text = tag.Text;
                        connectionChanged = true;
                    }
                }

                break;

            case CrudAction.Deleted:
                foreach (var tag in message.Tags)
                {
                    _cachedTags.Remove(tag);

                    foreach (var connection in _connections.Where(connection => connection.Tags.Contains(tag)))
                    {
                        connection.Tags.Remove(tag);
                        connectionChanged = true;
                    }
                }

                break;
        }
        return connectionChanged;
    }

    protected override ISuiteConnectionServiceResult CreateSuccessResult()
        => new SuiteConnectionServiceSuccessResult();

    protected override ISuiteConnectionServiceResult CreateErrorResult(string errorMessage, int? errorCode = null)
        => new SuiteConnectionServiceErrorResult(errorMessage, errorCode);
}
