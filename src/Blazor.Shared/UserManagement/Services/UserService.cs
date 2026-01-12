using System.Collections.Concurrent;
using Blazor.Shared.UserManagement.Contracts;
using Core.Shared.UserManagement.Commands;
using Core.Shared.UserManagement.Contracts;
using Core.Shared.UserManagement.Events;
using Core.Shared.UserManagement.Requests;
using Sdk.Client.Infrastructure;
using Sdk.Messaging;
using Sdk.Utils;
using ViciOne.Ui.Localization.Resources;

namespace Blazor.Shared.UserManagement.Services;

internal sealed class UserService : IUserService,
    IEventConsumer<UserCreatedEvent>,
    IEventConsumer<UserUpdatedEvent>,
    IEventConsumer<UserDeletedEvent>,
    IEventConsumer<UserErrorEvent>,
    IDisposable
{
    private readonly IUiMediator _mediator;
    private readonly AutoDisposeList<IDisposable> _subscriptions = [];

    private readonly ConcurrentDictionary<Guid, TaskCompletionSource<ErrorInfo?>> _taskCompletionSourceMap = new();

    public event Func<UserProfile, CrudAction, Task>? UserChanged;

    public UserService(IUiMediator mediator)
    {
        _mediator = mediator;

        _subscriptions.Add(_mediator.Register<UserCreatedEvent>(this));
        _subscriptions.Add(_mediator.Register<UserUpdatedEvent>(this));
        _subscriptions.Add(_mediator.Register<UserDeletedEvent>(this));
        _subscriptions.Add(_mediator.Register<UserErrorEvent>(this));
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

    public async Task<IUserServiceResult> CreateUser(UserProfile userProfile, CancellationToken cancellationToken = default)
    {
        var command = new CreateUser(userProfile);

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

    public async Task<IUserServiceResult> UpdateUser(UserProfile userProfile, CancellationToken cancellationToken = default)
    {
        var command = new UpdateUser(userProfile);

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

    public async Task<IUserServiceResult> DeleteUser(UserProfile userProfile, CancellationToken cancellationToken = default)
    {
        var command = new DeleteUser(userProfile);

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

    public async Task Consume(ClientContext<UserDeletedEvent> context, CancellationToken cancellationToken = default)
    {
        if (_taskCompletionSourceMap.TryRemove(context.Message.CorrelationId, out var taskCompletionSource))
            taskCompletionSource.SetResult(null);

        await NotifyUserChanged(context.Message.UserProfile, CrudAction.Deleted);
    }

    public async Task Consume(ClientContext<UserCreatedEvent> context, CancellationToken cancellationToken = default)
    {
        if (_taskCompletionSourceMap.TryRemove(context.Message.CorrelationId, out var taskCompletionSource))
            taskCompletionSource.SetResult(null);

        await NotifyUserChanged(context.Message.UserProfile, CrudAction.Created);
    }

    public async Task Consume(ClientContext<UserUpdatedEvent> context, CancellationToken cancellationToken = default)
    {
        if (_taskCompletionSourceMap.TryRemove(context.Message.CorrelationId, out var taskCompletionSource))
            taskCompletionSource.SetResult(null);

        await NotifyUserChanged(context.Message.UserProfile, CrudAction.Updated);
    }

    public Task Consume(ClientContext<UserErrorEvent> context, CancellationToken cancellationToken = default)
    {
        string errorMessage;

        if (context.Message.ErrorInfo.ErrorCode == UserErrorEvent.UpdateFailedPassword)
            errorMessage = string.Format(ValidationMessages.Culture, ValidationMessages.FieldDoesNotEqualToTheRecordedValue, Localization.Labels.CurrentPassword);
        else
            errorMessage = context.Message.ErrorInfo.Message ?? CommonPhrases.AnUnknownErrorOccurred;

        if (_taskCompletionSourceMap.TryRemove(context.Message.CorrelationId, out var taskCompletionSource))
            taskCompletionSource.SetResult(new ErrorInfo(context.Message.ErrorInfo.ErrorCode, errorMessage));

        return Task.CompletedTask;
    }

    public async Task<List<UserProfile>> GetUsers(UserName? userName = null, CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Request<GetUsers, GetUsersResponse>(new(userName), cancellationToken);

        return result.Users;
    }

    public async Task<List<string>> GetRoles(CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Request<GetRoles, GetRolesResponse>(new(), cancellationToken);

        return result.Roles;
    }

    private static async Task<IUserServiceResult> WaitForCommandCompletion(TaskCompletionSource<ErrorInfo?> taskCompletionSource,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var errorInfo = await taskCompletionSource.Task.WaitAsync(TimeSpan.FromMilliseconds(Constants.CommandTimeoutMs), cancellationToken);

            if (taskCompletionSource.Task.IsCanceled || errorInfo is null)
                return new UserServiceSuccessResult();

            return new UserServiceErrorResult(errorInfo.Message ?? CommonPhrases.AnUnknownErrorOccurred, errorInfo.ErrorCode);
        }
        catch (TimeoutException)
        {
            return new UserServiceErrorResult(CommonPhrases.TheOperationHasTimedOut);
        }
    }

    private async Task NotifyUserChanged(UserProfile userProfile, CrudAction crudAction)
    {
        if (UserChanged is not null)
            await UserChanged.Invoke(userProfile, crudAction);
    }
}
