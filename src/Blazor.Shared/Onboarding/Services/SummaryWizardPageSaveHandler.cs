using System.Collections.Concurrent;
using Blazor.Shared.Onboarding.Extensions;
using Blazor.Shared.Onboarding.Models;
using Blazor.Shared.UserManagement.Contracts;
using Blazor.Shared.UserManagement.Services;
using Core.Shared.HostManagement.Commands;
using Core.Shared.HostManagement.Events;
using Core.Shared.Instance.Commands;
using Core.Shared.Instance.Events;
using Core.Shared.Instance.Services;
using Core.Shared.UserManagement.Configuration;
using Core.Shared.UserManagement.Contracts;
using Microsoft.Extensions.Logging;
using Sdk.Client.Infrastructure;
using Sdk.Client.Wizards.Models;
using Sdk.Client.Wizards.Services;
using Sdk.Instance;
using Sdk.Messaging;
using Sdk.SystemConfiguration.Events;
using Sdk.Utils;

namespace Blazor.Shared.Onboarding.Services;

internal sealed partial class SummaryWizardPageSaveHandler : IWizardPageSaveHandler<SummaryWizardPageState>,
        IEventConsumer<SystemConfigurationChanged>,
        IEventConsumer<SetSystemConfigurationError>,
        IEventConsumer<CrossInstanceConfigurationChanged>,
        IEventConsumer<CrossInstanceConfigurationError>,
        IDisposable
{
    private readonly ITargetConfigurationProvider _targetConfigurationProvider;
    private readonly IUserService _userService;
    private readonly IAdministratorInitialPasswordProvider _administratorInitialPasswordProvider;
    private readonly IUiMediator _mediator;
    private readonly IOnboardingStateStore _onboardingStateStore;
    private readonly IInstanceInformationProvider _instanceInformationProvider;
    private readonly ILogger _logger;
    private readonly ConcurrentDictionary<Guid, TaskCompletionSource<ErrorInfo?>> _taskCompletionSourceMap = new();
    private readonly AutoDisposeList<IDisposable> _subscriptionHandles = [];

    public SummaryWizardPageSaveHandler(ITargetConfigurationProvider targetConfigurationProvider, IUserService userService,
        IAdministratorInitialPasswordProvider administratorInitialPasswordProvider,
        IUiMediator mediator, IOnboardingStateStore onboardingStateStore, IInstanceInformationProvider instanceInformationProvider,
        ILogger<SummaryWizardPageSaveHandler> logger)
    {
        _targetConfigurationProvider = targetConfigurationProvider;
        _userService = userService;
        _administratorInitialPasswordProvider = administratorInitialPasswordProvider;
        _mediator = mediator;
        _onboardingStateStore = onboardingStateStore;
        _instanceInformationProvider = instanceInformationProvider;
        _logger = logger;

        _subscriptionHandles.Add(mediator.Register<SystemConfigurationChanged>(this));
        _subscriptionHandles.Add(mediator.Register<SetSystemConfigurationError>(this));
        _subscriptionHandles.Add(mediator.Register<CrossInstanceConfigurationChanged>(this));
        _subscriptionHandles.Add(mediator.Register<CrossInstanceConfigurationError>(this));
    }

    public void Dispose()
    {
        _subscriptionHandles.Dispose();

        var correlationIds = _taskCompletionSourceMap.Keys;

        foreach (var correlationId in correlationIds)
        {
            if (_taskCompletionSourceMap.TryRemove(correlationId, out var taskCompletionSource))
                taskCompletionSource.SetCanceled();
        }

        _taskCompletionSourceMap.Clear();
    }

    public async Task<ISaveResult> Save(SummaryWizardPageState state, CancellationToken cancellationToken)
    {
        var targetConfiguration = await _targetConfigurationProvider.GetTargetConfiguration(cancellationToken);

        if (targetConfiguration.UserCredentials is not null)
        {
            var updatePasswordResult = await UpdatePassword(targetConfiguration.UserCredentials.Value, cancellationToken);
            if (!updatePasswordResult.Success)
                return new SaveErrorResult(updatePasswordResult.ErrorMessage);
        }

        var result = await SetTimeZone(targetConfiguration.TimeZone, cancellationToken);
        if (result is not SaveSuccessResult)
        {
            await SetOnboardingCompleted(false, cancellationToken);
            return result;
        }

        await SetOnboardingCompleted(true, cancellationToken);

        if (state.NewSystemConfiguration is not null)
        {
            state.BeginOperation(new WizardOperation { Description = Localization.SummaryWizardPageSaveHandler.ApplyingSystemConfiguration, EstimatedDurationMs = 10000 });
            try
            {
                var command = new SetSystemConfiguration(state.NewSystemConfiguration);

                var taskCompletionSource = new TaskCompletionSource<ErrorInfo?>();
                _taskCompletionSourceMap[command.CorrelationId] = taskCompletionSource;

                try
                {
                    await _mediator.Send(command, cancellationToken);

                    result = await taskCompletionSource.WaitForCommandCompletion(cancellationToken);
                    if (result is not SaveSuccessResult)
                    {
                        await SetOnboardingCompleted(false, cancellationToken);
                        return result;
                    }
                }
                finally
                {
                    _taskCompletionSourceMap.TryRemove(command.CorrelationId, out _);
                }
            }
            finally
            {
                state.EndOperation();
            }
        }

        return new SaveSuccessResult();
    }

    private readonly record struct OperationResult(bool Success, string ErrorMessage);

    private async Task<OperationResult> UpdatePassword(UserCredentials userCredentials, CancellationToken cancellationToken)
    {
        var users = await _userService.GetUsers(new UserName(userCredentials.UserName), cancellationToken);

        var user = users.FirstOrDefault();

        if (user is null)
        {
            var formatProvider = Localization.SummaryWizardPageSaveHandler.Culture;

            var errorMessage = string.Format(formatProvider, "{0} {1}", Localization.SummaryWizardPageSaveHandler.PasswordUpdateHasFailed,
                string.Format(formatProvider, Localization.ValidationMessages.UserWasNotFound, userCredentials.UserName));

            return new OperationResult { ErrorMessage = errorMessage };
        }

        user.CurrentPassword = _administratorInitialPasswordProvider.GetAdministratorInitialPassword();
        user.NewPassword = userCredentials.Password;

        var result = await _userService.UpdateUser(user, cancellationToken);

        if (result is UserManagementServiceErrorResult errorResult)
        {
            var formatProvider = Localization.SummaryWizardPageSaveHandler.Culture;

            var errorMessage = string.Format(formatProvider, "{0} {1}", Localization.SummaryWizardPageSaveHandler.PasswordUpdateHasFailed,
                errorResult.ErrorMessage);

            return new OperationResult { ErrorMessage = errorMessage };
        }

        return new OperationResult { Success = true };
    }

    private async Task<ISaveResult> SetTimeZone(TimeZoneInfo timeZone, CancellationToken cancellationToken)
    {
        var command = new SetCrossInstanceConfiguration(null, timeZone.Id);
        var taskCompletionSource = new TaskCompletionSource<ErrorInfo?>();

        _taskCompletionSourceMap[command.CorrelationId] = taskCompletionSource;

        try
        {
            await _mediator.Send(command, cancellationToken);

            var saveResult = await taskCompletionSource.WaitForCommandCompletion(cancellationToken);
            return saveResult;
        }
        finally
        {
            _taskCompletionSourceMap.TryRemove(command.CorrelationId, out _);
        }
    }

    private async Task SetOnboardingCompleted(bool value, CancellationToken cancellationToken)
    {
        var instanceId = _instanceInformationProvider.Local.Id;

        var onboardingState = await _onboardingStateStore.GetOnboardingStateAsync(instanceId, cancellationToken);

        onboardingState.Completed = value;

        await _onboardingStateStore.SetOnboardingStateAsync(onboardingState, cancellationToken);
    }

    public Task Consume(ClientContext<CrossInstanceConfigurationChanged> context, CancellationToken cancellationToken)
    {
        if (_taskCompletionSourceMap.TryRemove(context.Message.CorrelationId, out var taskCompletionSource))
            taskCompletionSource.SetResult(null);

        return Task.CompletedTask;
    }

    public Task Consume(ClientContext<CrossInstanceConfigurationError> context, CancellationToken cancellationToken = default)
    {
        if (_taskCompletionSourceMap.TryRemove(context.Message.CorrelationId, out var taskCompletionSource))
            taskCompletionSource.SetResult(context.Message.Error);

        return Task.CompletedTask;
    }

    public Task Consume(ClientContext<SystemConfigurationChanged> context, CancellationToken cancellationToken)
    {
        if (_taskCompletionSourceMap.TryRemove(context.Message.CorrelationId, out var taskCompletionSource))
            taskCompletionSource.SetResult(null);

        SetSystemConfigurationSuccess(_logger);

        return Task.CompletedTask;
    }

    public Task Consume(ClientContext<SetSystemConfigurationError> context, CancellationToken cancellationToken)
    {
        if (_taskCompletionSourceMap.TryRemove(context.Message.CorrelationId, out var taskCompletionSource))
            taskCompletionSource.SetResult(context.Message.Error);

        SetSystemConfigurationFailed(_logger, context.Message.Error);

        return Task.CompletedTask;
    }

    [LoggerMessage(1, LogLevel.Information, "Save invoked")]
    private static partial void SaveInvoked(ILogger logger);

    [LoggerMessage(2, LogLevel.Information, "Set system configuration successfully set")]
    private static partial void SetSystemConfigurationSuccess(ILogger logger);

    [LoggerMessage(3, LogLevel.Error, "Set system configuration failed: {error}")]
    private static partial void SetSystemConfigurationFailed(ILogger logger, ErrorInfo error);
}
