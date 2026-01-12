using System.Collections.Concurrent;
using Blazor.Shared.UserManagement.Contracts;
using Microsoft.Extensions.Logging;
using Sdk.Client.Infrastructure;
using Sdk.Messaging;
using Sdk.UserManagement.Commands;
using Sdk.UserManagement.Contracts;
using Sdk.UserManagement.Events;
using Sdk.UserManagement.Requests;
using Sdk.Utils;
using ViciOne.Ui.Localization.Resources;

namespace Blazor.Shared.UserManagement.Services;

internal sealed class RoleService : IRoleService,
    IEventConsumer<RoleCreatedEvent>,
    IEventConsumer<RoleUpdatedEvent>,
    IEventConsumer<RoleDeletedEvent>,
    IEventConsumer<RoleErrorEvent>,
    IDisposable
{
    private readonly ConcurrentDictionary<Guid, TaskCompletionSource<ErrorInfo?>> _taskCompletionSourceMap = new();
    private readonly IUiMediator _mediator;
    private readonly ILogger<RoleService> _logger;
    private readonly AutoDisposeList<IDisposable> _subscriptions = [];

    public RoleService(IUiMediator mediator, ILogger<RoleService> logger)
    {
        _mediator = mediator;
        _logger = logger;

        _subscriptions.Add(_mediator.Register<RoleCreatedEvent>(this));
        _subscriptions.Add(_mediator.Register<RoleUpdatedEvent>(this));
        _subscriptions.Add(_mediator.Register<RoleDeletedEvent>(this));
        _subscriptions.Add(_mediator.Register<RoleErrorEvent>(this));
    }

    public async Task<IUserManagementServiceResult> CreateRole(Role role, CancellationToken cancellationToken = default)
    {
        var command = new CreateRole(role);

        var taskCompletionSource = new TaskCompletionSource<ErrorInfo?>();
        _taskCompletionSourceMap[command.CorrelationId] = taskCompletionSource;

        try
        {
            await _mediator.Send(command, cancellationToken);

            return await WaitForCommandCompletion(taskCompletionSource, cancellationToken);
        }
        finally
        {
            _taskCompletionSourceMap.TryRemove(command.CorrelationId, out _);
        }
    }

    public async Task<IUserManagementServiceResult> DeleteRole(Role role, CancellationToken cancellationToken = default)
    {
        var command = new DeleteRole(role);

        var taskCompletionSource = new TaskCompletionSource<ErrorInfo?>();
        _taskCompletionSourceMap[command.CorrelationId] = taskCompletionSource;

        try
        {
            await _mediator.Send(command, cancellationToken);

            return await WaitForCommandCompletion(taskCompletionSource, cancellationToken);
        }
        finally
        {
            _taskCompletionSourceMap.TryRemove(command.CorrelationId, out _);
        }
    }

    public async Task<IEnumerable<Role>> GetAvailableRoles(CancellationToken cancellationToken = default)
    {
        var rolesResponse = await _mediator.Request<GetRoles, GetRolesResponse>(new(), cancellationToken);
        if (rolesResponse.RequestError is not null)
        {
            _logger.LogError("Could not load available Roles - {ErrorMessage}", rolesResponse.RequestError.Message);
            return [];
        }

        return rolesResponse.Roles;
    }

    public async Task<IUserManagementServiceResult> UpdateRole(Role role, CancellationToken cancellationToken = default)
    {
        var command = new UpdateRole(role);

        var taskCompletionSource = new TaskCompletionSource<ErrorInfo?>();
        _taskCompletionSourceMap[command.CorrelationId] = taskCompletionSource;

        try
        {
            await _mediator.Send(command, cancellationToken);

            return await WaitForCommandCompletion(taskCompletionSource, cancellationToken);
        }
        finally
        {
            _taskCompletionSourceMap.TryRemove(command.CorrelationId, out _);
        }
    }

    private static async Task<IUserManagementServiceResult> WaitForCommandCompletion(TaskCompletionSource<ErrorInfo?> taskCompletionSource,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var errorInfo = await taskCompletionSource.Task.WaitAsync(TimeSpan.FromMilliseconds(Constants.CommandTimeoutMs), cancellationToken);

            if (taskCompletionSource.Task.IsCanceled || errorInfo is null)
                return new UserManagementServiceSuccessResult();

            return new UserManagementServiceErrorResult(errorInfo.Message ?? CommonPhrases.AnUnknownErrorOccurred, errorInfo.ErrorCode);
        }
        catch (TimeoutException)
        {
            return new UserManagementServiceErrorResult(CommonPhrases.TheOperationHasTimedOut);
        }
    }

    public Task Consume(ClientContext<RoleCreatedEvent> context, CancellationToken cancellationToken)
    {
        if (_taskCompletionSourceMap.TryRemove(context.Message.CorrelationId, out var taskCompletionSource))
            taskCompletionSource.SetResult(null);

        return Task.CompletedTask;
    }

    public Task Consume(ClientContext<RoleUpdatedEvent> context, CancellationToken cancellationToken)
    {
        if (_taskCompletionSourceMap.TryRemove(context.Message.CorrelationId, out var taskCompletionSource))
            taskCompletionSource.SetResult(null);

        return Task.CompletedTask;
    }

    public Task Consume(ClientContext<RoleDeletedEvent> context, CancellationToken cancellationToken)
    {
        if (_taskCompletionSourceMap.TryRemove(context.Message.CorrelationId, out var taskCompletionSource))
            taskCompletionSource.SetResult(null);

        return Task.CompletedTask;
    }

    public Task Consume(ClientContext<RoleErrorEvent> context, CancellationToken cancellationToken)
    {
        string errorMessage;

        errorMessage = context.Message.ErrorInfo.Message ?? CommonPhrases.AnUnknownErrorOccurred;

        if (_taskCompletionSourceMap.TryRemove(context.Message.CorrelationId, out var taskCompletionSource))
            taskCompletionSource.SetResult(new ErrorInfo(context.Message.ErrorInfo.ErrorCode, errorMessage));

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _subscriptions.Dispose();

        var correlationIds = _taskCompletionSourceMap.Keys;

        foreach (var correlationId in correlationIds)
        {
            if (_taskCompletionSourceMap.TryRemove(correlationId, out var taskCompletionSource))
                taskCompletionSource.SetCanceled();
        }

        _taskCompletionSourceMap.Clear();
    }
}
