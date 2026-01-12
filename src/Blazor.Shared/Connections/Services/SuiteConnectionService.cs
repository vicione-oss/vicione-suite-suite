using System.Collections.Concurrent;
using System.ComponentModel;
using Blazor.Shared.Connections.Contracts;
using Sdk.Client.Infrastructure;
using Sdk.Connections.Commands;
using Sdk.Connections.Contracts;
using Sdk.Connections.Events;
using Sdk.Connections.Extensions;
using Sdk.Connections.Requests;
using Sdk.Messaging;
using Sdk.Utils;
using ViciOne.Ui.Localization.Resources;

namespace Blazor.Shared.Connections.Services;

internal sealed class SuiteConnectionService : ISuiteConnectionService,
    IEventConsumer<ConnectionChanged>,
    IEventConsumer<ConnectionErrorOccured>,
    IEventConsumer<TagsChanged>,
    IDisposable
{
    private bool _initialized;
    private readonly IUiMediator _mediator;
    private List<Connection> _connections = [];
    private readonly HashSet<Tag> _tags = [];
    private readonly AutoDisposeList<IDisposable> _subscriptions = [];

    private readonly ConcurrentDictionary<Guid, TaskCompletionSource<ErrorInfo?>> _connectionTaskCompletionSourceMap = new();
    private readonly ConcurrentDictionary<Guid, TaskCompletionSource<ErrorInfo?>> _tagTaskCompletionSourceMap = new();

    public IReadOnlySet<Tag> Tags => _tags;
    public IReadOnlyList<Connection> Connections => _connections;

    public event Func<IReadOnlyList<Connection>, Task>? ConnectionStateChanged;

    public SuiteConnectionService(IUiMediator mediator)
    {
        _mediator = mediator;

        _subscriptions.Add(_mediator.Register<ConnectionChanged>(this));
        _subscriptions.Add(_mediator.Register<ConnectionErrorOccured>(this));
        _subscriptions.Add(_mediator.Register<TagsChanged>(this));
    }

    public void Dispose()
    {
        _subscriptions.Dispose();

        var correlationIds = _tagTaskCompletionSourceMap.Keys;

        foreach (var correlationId in correlationIds)
        {
            if (_tagTaskCompletionSourceMap.TryRemove(correlationId, out var taskCompletionSource))
                taskCompletionSource.SetCanceled();
        }

        _tagTaskCompletionSourceMap.Clear();
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
        var response = await _mediator.Request<GetConnections, GetConnectionsResponse>(new(connectionId, null), cancellationToken);

        return response.Connections.FirstOrDefault();
    }

    public async Task<List<Connection>> GetConnections(List<string>? filterTypes, CancellationToken cancellationToken = default)
    {
        var response = await _mediator.Request<GetConnections, GetConnectionsResponse>(new(null, filterTypes), cancellationToken);

        return response.Connections;
    }

    public async Task<Tag?> GetTag(Guid tagId, CancellationToken cancellationToken = default)
    {
        var tags = await GetTags(cancellationToken);

        return tags.FirstOrDefault(t => t.Id == tagId);
    }

    public async Task<List<Tag>> GetTags(CancellationToken cancellationToken = default)
    {
        var response = await _mediator.Request<GetTags, GetTagsResponse>(new(), cancellationToken);

        return response.Tags;
    }

    public async Task<ISuiteConnectionServiceResult> UpsertConnection(Connection connection, CancellationToken cancellationToken = default)
    {
        var command = new UpsertConnection(connection);

        var taskCompletionSource = new TaskCompletionSource<ErrorInfo?>();
        _connectionTaskCompletionSourceMap[command.CorrelationId] = taskCompletionSource;

        try
        {
            await _mediator.Send(command, cancellationToken);

            return await WaitForCommandCompletion(taskCompletionSource, cancellationToken);
        }
        finally
        {
            _connectionTaskCompletionSourceMap.TryRemove(command.CorrelationId, out _);
        }
    }

    public async Task<ISuiteConnectionServiceResult> DeleteConnection(Connection connection, CancellationToken cancellationToken = default)
    {
        var command = new DeleteConnection(connection.Id);

        var taskCompletionSource = new TaskCompletionSource<ErrorInfo?>();
        _connectionTaskCompletionSourceMap[command.CorrelationId] = taskCompletionSource;

        try
        {
            await _mediator.Send(command, cancellationToken);

            return await WaitForCommandCompletion(taskCompletionSource, cancellationToken);
        }
        finally
        {
            _connectionTaskCompletionSourceMap.TryRemove(command.CorrelationId, out _);
        }
    }

    public async Task Consume(ClientContext<ConnectionChanged> context, CancellationToken cancellationToken = default)
    {
        if (!_initialized)
            return;

        var somethingChanged = false;
        var connection = context.Message.Connection;

        switch (context.Message.Action)
        {
            case CrudAction.Created:
                foreach (var tag in context.Message.AddedTags)
                    somethingChanged |= _tags.Add(tag);

                if (!_connections.Any(c => c.Id == connection.Id))
                {
                    _connections.Add(connection);

                    somethingChanged = true;
                }

                break;

            case CrudAction.Updated:
                foreach (var tag in context.Message.AddedTags)
                    somethingChanged |= _tags.Add(tag);

                var existingConnection = _connections.FirstOrDefault(c => c.Id == connection.Id);
                if (existingConnection is not null)
                {
                    existingConnection.Assign(connection);

                    somethingChanged = true;
                }

                break;

            case CrudAction.Deleted:
                somethingChanged = _connections.RemoveAll(c => c.Id == connection.Id) > 0;

                break;

            default:
                throw new InvalidEnumArgumentException(nameof(context.Message.Action), (int)context.Message.Action, typeof(CrudAction));
        }

        if (_connectionTaskCompletionSourceMap.TryRemove(context.Message.CorrelationId, out var taskCompletionSource))
            taskCompletionSource.SetResult(null);

        if (somethingChanged)
            await NotifyConnectionStateChanged();
    }

    private async Task NotifyConnectionStateChanged()
    {
        if (ConnectionStateChanged is not null)
            await ConnectionStateChanged.Invoke(_connections);
    }

    public Task Consume(ClientContext<ConnectionErrorOccured> context, CancellationToken cancellationToken = default)
    {
        if (_connectionTaskCompletionSourceMap.TryRemove(context.Message.CorrelationId, out var taskCompletionSource))
            taskCompletionSource.SetResult(context.Message.Error);

        return Task.CompletedTask;
    }

    public async Task<ISuiteConnectionServiceResult> UpsertTag(Tag tag, CancellationToken cancellationToken = default)
    {
        var command = new UpsertTag(tag);

        var taskCompletionSource = new TaskCompletionSource<ErrorInfo?>();
        _tagTaskCompletionSourceMap[command.CorrelationId] = taskCompletionSource;

        try
        {
            await _mediator.Send(command, cancellationToken);

            return await WaitForCommandCompletion(taskCompletionSource, cancellationToken);
        }
        finally
        {
            _tagTaskCompletionSourceMap.TryRemove(command.CorrelationId, out _);
        }
    }

    public async Task<ISuiteConnectionServiceResult> DeleteTag(Tag tag, CancellationToken cancellationToken = default)
    {
        if (tag.Protected)
            return new SuiteConnectionServiceErrorResult(Localization.SuiteConnectionService.TagCannotBeDeletedBecauseItIsProtected);

        var command = new DeleteTag(tag.Id);

        var taskCompletionSource = new TaskCompletionSource<ErrorInfo?>();
        _tagTaskCompletionSourceMap[command.CorrelationId] = taskCompletionSource;

        try
        {
            await _mediator.Send(command, cancellationToken);

            return await WaitForCommandCompletion(taskCompletionSource, cancellationToken);
        }
        finally
        {
            _tagTaskCompletionSourceMap.TryRemove(command.CorrelationId, out _);
        }
    }

    public Task Consume(ClientContext<TagsChanged> context, CancellationToken cancellationToken = default)
    {
        if (!_initialized)
            return Task.CompletedTask;
        var connectionChanged = false;
        switch (context.Message.Action)
        {
            case CrudAction.Created:
                foreach (var tag in context.Message.Tags)
                    _tags.Add(tag);

                break;

            case CrudAction.Updated:
                foreach (var tag in context.Message.Tags)
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
                foreach (var tag in context.Message.Tags)
                {
                    _tags.Remove(tag);
                    foreach (var connection in _connections.Where(connection => connection.Tags.Contains(tag)))
                    {
                        connection.Tags.Remove(tag);
                        connectionChanged = true;
                    }
                }

                break;

            default:
                throw new InvalidEnumArgumentException(nameof(context.Message.Action), (int)context.Message.Action, typeof(CrudAction));
        }

        if (_tagTaskCompletionSourceMap.TryRemove(context.Message.CorrelationId, out var deleteTagTaskCompletionSource))
            deleteTagTaskCompletionSource.SetResult(null);

        return connectionChanged ? NotifyConnectionStateChanged() : Task.CompletedTask;
    }

    private async Task<ISuiteConnectionServiceResult> WaitForCommandCompletion(TaskCompletionSource<ErrorInfo?> taskCompletionSource,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var errorInfo = await taskCompletionSource.Task.WaitAsync(TimeSpan.FromMilliseconds(Constants.CommandTimeoutMs), cancellationToken);

            if (taskCompletionSource.Task.IsCanceled)
                return new SuiteConnectionServiceSuccessResult();

            if (errorInfo is null)
                return new SuiteConnectionServiceSuccessResult();

            return new SuiteConnectionServiceErrorResult(errorInfo.Message ?? CommonPhrases.AnUnknownErrorOccurred, errorInfo.ErrorCode);
        }
        catch (TimeoutException)
        {
            return new SuiteConnectionServiceErrorResult(CommonPhrases.TheOperationHasTimedOut);
        }
    }
}
