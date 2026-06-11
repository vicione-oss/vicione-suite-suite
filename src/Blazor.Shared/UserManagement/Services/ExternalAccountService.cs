using Blazor.Shared.Profile.Localization;
using Blazor.Shared.UserManagement.Contracts;
using Core.Shared.UserManagement.Commands;
using Core.Shared.UserManagement.Contracts;
using Core.Shared.UserManagement.Events;
using Microsoft.AspNetCore.Identity;
using Sdk.Client.ControlPanels.Services;
using Sdk.Client.Infrastructure;
using Sdk.Messaging;

namespace Blazor.Shared.UserManagement.Services;

public sealed class ExternalAccountService :
    CompletionSourceHandlerBase<IUserManagementServiceResult>,
    IExternalAccountService,
    IEventConsumer<ExternalLoginDeletionCompleted>
{
    private readonly UserManager<SuiteUser> _userManager;

    public ExternalAccountService(IUiMediator mediator, UserManager<SuiteUser> userManager) : base(mediator)
    {
        _userManager = userManager;

        Register<ExternalLoginDeletionCompleted>();
    }

    public async Task<ExternalUserAccount?> GetExternalUserAccount(SuiteUser user)
    {
        var userLoginInfos = await _userManager.GetLoginsAsync(user);

        return userLoginInfos
            .Select(info => new ExternalUserAccount(info.LoginProvider, info.ProviderDisplayName, info.ProviderKey))
            .SingleOrDefault();
    }

    public async Task<IUserManagementServiceResult> RemoveExternalAccount(SuiteUser user,
        string loginProvider,
        string providerKey,
        CancellationToken cancellationToken = default)
    {
        var command = new DeleteExternalLogin
        {
            UserId = user.Id,
            LoginProvider = loginProvider,
            ProviderKey = providerKey
        };

        return await SendAndWaitForCompletion(command, BuildErrorResult, cancellationToken);
    }

    private static IUserManagementServiceResult BuildErrorResult(ErrorInfo errorInfo)
    {
        var message = (ExternalLoginError)errorInfo.ErrorCode switch
        {
            ExternalLoginError.LastCredential => ExternalIdProviders.UnlinkFailedLastCredential,
            _ => ExternalIdProviders.UnlinkFailed
        };

        return new UserManagementServiceErrorResult(message, errorInfo.ErrorCode);
    }

    public Task Consume(ClientContext<ExternalLoginDeletionCompleted> context, CancellationToken cancellationToken)
    {
        if (context.Message.ErrorInfo is null)
            CompleteWithSuccess(context.Message.CorrelationId);
        else
            CompleteWithError(context.Message.CorrelationId, context.Message.ErrorInfo);

        return Task.CompletedTask;
    }

    protected override IUserManagementServiceResult CreateSuccessResult()
        => new UserManagementServiceSuccessResult();

    protected override IUserManagementServiceResult CreateErrorResult(string errorMessage, int? errorCode = null)
        => new UserManagementServiceErrorResult(errorMessage, errorCode);
}
