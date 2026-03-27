using Blazor.Shared.UserManagement.Contracts;
using Microsoft.Extensions.Logging;
using Sdk.Client.ControlPanels.Services;
using Sdk.Client.Infrastructure;
using Sdk.UserManagement.Commands;
using Sdk.UserManagement.Contracts;
using Sdk.UserManagement.Events;
using Sdk.UserManagement.Requests;

namespace Blazor.Shared.UserManagement.Services;

internal sealed class RoleService : CompletionSourceHandlerBase<IUserManagementServiceResult>, IRoleService,
    IEventConsumer<RoleCreatedEvent>,
    IEventConsumer<RoleUpdatedEvent>,
    IEventConsumer<RoleDeletedEvent>
{
    private readonly ILogger<RoleService> _logger;

    public RoleService(IUiMediator mediator, ILogger<RoleService> logger) : base(mediator)
    {
        _logger = logger;

        Register<RoleCreatedEvent>();
        Register<RoleUpdatedEvent>();
        Register<RoleDeletedEvent>();
    }

    public async Task<IUserManagementServiceResult> CreateRole(Role role, CancellationToken cancellationToken = default)
    {
        var command = new CreateRole(role);

        return await SendAndWaitForCompletion(command, cancellationToken);
    }

    public async Task<IUserManagementServiceResult> DeleteRole(Role role, CancellationToken cancellationToken = default)
    {
        var command = new DeleteRole(role);

        return await SendAndWaitForCompletion(command, cancellationToken);
    }

    public async Task<IEnumerable<Role>> GetAvailableRoles(CancellationToken cancellationToken = default)
    {
        var rolesResponse = await Mediator.Request<GetRoles, GetRolesResponse>(new(), cancellationToken);
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

        return await SendAndWaitForCompletion(command, cancellationToken);
    }

    public Task Consume(ClientContext<RoleCreatedEvent> context, CancellationToken cancellationToken)
    {
        if (context.Message.ErrorInfo is not null)
        {
            CompleteWithError(context.Message.CorrelationId, context.Message.ErrorInfo);
            return Task.CompletedTask;
        }

        CompleteWithSuccess(context.Message.CorrelationId);

        return Task.CompletedTask;
    }

    public Task Consume(ClientContext<RoleUpdatedEvent> context, CancellationToken cancellationToken)
    {
        if (context.Message.ErrorInfo is not null)
        {
            CompleteWithError(context.Message.CorrelationId, context.Message.ErrorInfo);
            return Task.CompletedTask;
        }

        CompleteWithSuccess(context.Message.CorrelationId);

        return Task.CompletedTask;
    }

    public Task Consume(ClientContext<RoleDeletedEvent> context, CancellationToken cancellationToken)
    {
        if (context.Message.ErrorInfo is not null)
        {
            CompleteWithError(context.Message.CorrelationId, context.Message.ErrorInfo);
            return Task.CompletedTask;
        }

        CompleteWithSuccess(context.Message.CorrelationId);

        return Task.CompletedTask;
    }

    protected override IUserManagementServiceResult CreateSuccessResult()
        => new UserManagementServiceSuccessResult();

    protected override IUserManagementServiceResult CreateErrorResult(string errorMessage, int? errorCode = null)
        => new UserManagementServiceErrorResult(errorMessage, errorCode);
}
