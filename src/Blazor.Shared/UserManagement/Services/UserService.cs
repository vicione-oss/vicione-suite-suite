using Blazor.Shared.UserManagement.Contracts;
using Core.Shared.UserManagement.Commands;
using Core.Shared.UserManagement.Contracts;
using Core.Shared.UserManagement.Events;
using Core.Shared.UserManagement.Requests;
using Microsoft.AspNetCore.Components.Authorization;
using Sdk.Client.ControlPanels.Services;
using Sdk.Client.Infrastructure;
using Sdk.Messaging;
using Sdk.UserManagement.Requests;
using ViciOne.Ui.Localization.Resources;

namespace Blazor.Shared.UserManagement.Services;

internal sealed class UserService : CompletionSourceHandlerBase<IUserManagementServiceResult>,
    IUserService,
    IEventConsumer<UserCreatedEvent>,
    IEventConsumer<UserUpdatedEvent>,
    IEventConsumer<UserDeletedEvent>
{
    private readonly AuthenticationStateProvider _authenticationStateProvider;
    private string? _currentUserName;

    public event Func<UserProfile, CrudAction, Task>? UserChanged;

    public UserService(IUiMediator mediator, AuthenticationStateProvider authenticationStateProvider) : base(mediator)
    {
        _authenticationStateProvider = authenticationStateProvider;

        Register<UserCreatedEvent>();
        Register<UserUpdatedEvent>();
        Register<UserDeletedEvent>();
    }

    public async Task<IUserManagementServiceResult> CreateUser(UserProfile userProfile, CancellationToken cancellationToken = default)
    {
        var command = new CreateUser(userProfile);

        return await SendAndWaitForCompletion(command, cancellationToken);
    }

    public async Task<IUserManagementServiceResult> UpdateUser(UserProfile userProfile, CancellationToken cancellationToken = default)
    {
        _currentUserName ??= (await _authenticationStateProvider.GetAuthenticationStateAsync()).User.Identity?.Name;
        var command = new UpdateUser(userProfile, _currentUserName ?? throw new InvalidOperationException());

        return await SendAndWaitForCompletion(command, cancellationToken);
    }

    public async Task<IUserManagementServiceResult> DeleteUser(UserProfile userProfile, CancellationToken cancellationToken = default)
    {
        var command = new DeleteUser(userProfile);

        return await SendAndWaitForCompletion(command, cancellationToken);
    }

    public async Task Consume(ClientContext<UserDeletedEvent> context, CancellationToken cancellationToken = default)
    {
        if (context.Message.ErrorInfo is not null)
        {
            HandleError(context.Message.CorrelationId, context.Message.ErrorInfo);
            return;
        }

        CompleteWithSuccess(context.Message.CorrelationId);

        await NotifyUserChanged(context.Message.UserProfile, CrudAction.Deleted);
    }

    public async Task Consume(ClientContext<UserCreatedEvent> context, CancellationToken cancellationToken = default)
    {
        if (context.Message.ErrorInfo is not null)
        {
            HandleError(context.Message.CorrelationId, context.Message.ErrorInfo);
            return;
        }

        CompleteWithSuccess(context.Message.CorrelationId);

        await NotifyUserChanged(context.Message.UserProfile, CrudAction.Created);
    }

    public async Task Consume(ClientContext<UserUpdatedEvent> context, CancellationToken cancellationToken = default)
    {
        if (context.Message.ErrorInfo is not null)
        {
            HandleError(context.Message.CorrelationId, context.Message.ErrorInfo);
            return;
        }

        CompleteWithSuccess(context.Message.CorrelationId);

        await NotifyUserChanged(context.Message.UserProfile, CrudAction.Updated);
    }

    public void HandleError(Guid correlationId, ErrorInfo errorInfo)
    {
        string errorMessage;

        if (errorInfo.ErrorCode == UserErrorCodes.UpdateFailedPassword)
            errorMessage = string.Format(ValidationMessages.Culture, ValidationMessages.FieldDoesNotEqualToTheRecordedValue, Localization.Labels.CurrentPassword);
        else
            errorMessage = errorInfo.Message ?? CommonPhrases.AnUnknownErrorOccurred;

        CompleteWithError(correlationId, new ErrorInfo(errorInfo.ErrorCode, errorMessage));
    }

    public async Task<List<UserProfile>> GetUsers(UserName? userName = null, CancellationToken cancellationToken = default)
    {
        var result = await Mediator.Request<GetUsers, GetUsersResponse>(new(userName), cancellationToken);

        return result.Users;
    }

    public async Task<List<Sdk.UserManagement.Contracts.Role>> GetRoles(CancellationToken cancellationToken = default)
    {
        var result = await Mediator.Request<GetRoles, GetRolesResponse>(new(), cancellationToken);

        return result.Roles;
    }

    protected override IUserManagementServiceResult CreateSuccessResult()
        => new UserManagementServiceSuccessResult();

    protected override IUserManagementServiceResult CreateErrorResult(string errorMessage, int? errorCode = null)
        => new UserManagementServiceErrorResult(errorMessage, errorCode);

    private async Task NotifyUserChanged(UserProfile userProfile, CrudAction crudAction)
    {
        if (UserChanged is not null)
            await UserChanged.Invoke(userProfile, crudAction);
    }
}
