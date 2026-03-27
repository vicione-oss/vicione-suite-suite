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
    private readonly HashSet<Tag> _tags = [];
    public IReadOnlySet<Tag> Tags => _tags;
    public IReadOnlyList<Connection> Connections => _connections;

    public event Func<IReadOnlyList<Connection>, Task>? ConnectionStateChanged;

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
                _tags.Add(tag);

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
            return;
        }

        var somethingChanged = false;

        try
        {
            somethingChanged = ApplyConnectionChange(context.Message);
        }
        finally
        {
            CompleteWithSuccess(context.Message.CorrelationId);
        }

        if (somethingChanged)
            await NotifyConnectionStateChanged();
    }

    private bool ApplyConnectionChange(ConnectionChanged message)
    {
        var somethingChanged = false;

        switch (message.Action)
        {
            case CrudAction.Created:
                foreach (var tag in message.AddedTags)
                    somethingChanged |= _tags.Add(tag);

                if (!_connections.Any(c => c.Id == message.Connection.Id))
                {
                    _connections.Add(message.Connection);

                    somethingChanged = true;
                }

                break;

            case CrudAction.Updated:
                foreach (var tag in message.AddedTags)
                    somethingChanged |= _tags.Add(tag);

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

    public Task Consume(ClientContext<TagsChanged> context, CancellationToken cancellationToken = default)
    {
        if (!_initialized)
            return Task.CompletedTask;

        if (context.Message.ErrorInfo is not null)
        {
            CompleteWithError(context.Message.CorrelationId, context.Message.ErrorInfo);
            return Task.CompletedTask;
        }

        var connectionChanged = false;

        try
        {
            connectionChanged = ApplyTagChange(context.Message);
        }
        finally
        {
            CompleteWithSuccess(context.Message.CorrelationId);
        }

        return connectionChanged ? NotifyConnectionStateChanged() : Task.CompletedTask;
    }

    private bool ApplyTagChange(TagsChanged message)
    {
        var connectionChanged = false;
        switch (message.Action)
        {
            case CrudAction.Created:
                foreach (var tag in message.Tags)
                    _tags.Add(tag);

                break;

            case CrudAction.Updated:
                foreach (var tag in message.Tags)
                {
                    if (_tags.TryGetValue(tag, out var existing))
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
                    _tags.Remove(tag);

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
