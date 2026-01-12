using System.Collections.Concurrent;
using Blazor.Shared.Extensions;
using Blazor.Shared.Validation.Services.Validators;
using Core.Shared.HostManagement;
using Core.Shared.HostManagement.Commands;
using Core.Shared.HostManagement.Events;
using Microsoft.Extensions.Logging;
using Sdk.Client.ControlPanels.Models;
using Sdk.Client.ControlPanels.Services;
using Sdk.Client.Infrastructure;
using Sdk.Instance;
using Sdk.Messaging;
using Sdk.Utils;
using ViciOne.Ui.Localization.Resources;

namespace Blazor.Shared.Instance.Services;

internal sealed partial class UpdateControlPanelSaveHandler : IControlPanelSaveHandler<UpdateControlPanelState>,
    IEventConsumer<InstallSuiteVersionStarted>, IEventConsumer<InstallSuiteVersionError>,
    IEventConsumer<UpdateSystemStarted>, IEventConsumer<UpdateSystemError>,
    IDisposable
{
    private readonly AutoDisposeList<IDisposable> _subscriptionHandles = [];
    private readonly IUiMediator _mediator;
    private readonly IInstanceInformationProvider _instanceInformationProvider;
    private readonly IRequiredValidator _requiredValidator;
    private readonly ILogger<UpdateControlPanelSaveHandler> _logger;
    private readonly ConcurrentDictionary<Guid, TaskCompletionSource<ErrorInfo?>> _taskCompletionSourceMap = new();

    public UpdateControlPanelSaveHandler(IUiMediator mediator, IInstanceInformationProvider instanceInformationProvider,
        IRequiredValidator requiredValidator, ILogger<UpdateControlPanelSaveHandler> logger)
    {
        _mediator = mediator;
        _instanceInformationProvider = instanceInformationProvider;
        _requiredValidator = requiredValidator;
        _logger = logger;

        _subscriptionHandles.Add(_mediator.Register<InstallSuiteVersionStarted>(this));
        _subscriptionHandles.Add(_mediator.Register<InstallSuiteVersionError>(this));
        _subscriptionHandles.Add(_mediator.Register<UpdateSystemStarted>(this));
        _subscriptionHandles.Add(_mediator.Register<UpdateSystemError>(this));
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

    public async Task<ISaveResult> Save(UpdateControlPanelState state, CancellationToken cancellationToken)
    {
        if (state.SelectedVersion is not null && state.SelectedVersionChanged && state.FlashDeviceEnabled)
            return new SaveErrorResult(Localization.UpdateControlPanelSaveHandler.InstallVersionAndUpdateSystemNotAllowedAtTheSameTime);

        if (state.SelectedVersion is not null && state.SelectedVersionChanged)
        {
            var installVersion = state.SuiteVersions?.FirstOrDefault(k => k.Version == state.SelectedVersion)
                ?? throw new InvalidOperationException("Selected version not found in available suite versions.");

            var command = new InstallSuiteVersion(installVersion.PackageName, installVersion.SignatureName);

            var taskCompletionSource = new TaskCompletionSource<ErrorInfo?>();
            _taskCompletionSourceMap[command.CorrelationId] = taskCompletionSource;

            try
            {
                await _mediator.Send(command, cancellationToken);

                var result = await taskCompletionSource.WaitForCommandCompletion(CreateInstallOfSelectedVersionFailedResult, cancellationToken);
                if (result is SaveSuccessResult)
                    state.SelectedVersionChanged = false;

                return result;
            }
            finally
            {
                _taskCompletionSourceMap.TryRemove(command.CorrelationId, out _);
            }
        }

        if (state.FlashDeviceEnabled)
        {
            if (!_requiredValidator.Validate(state.SwuFilenameUploaded, TechnicalTerms.Image, out var errorMessage))
                return new SaveErrorResult(errorMessage);

            var command = new UpdateSystem(state.SwuFilenameUploaded);

            var taskCompletionSource = new TaskCompletionSource<ErrorInfo?>();
            _taskCompletionSourceMap[command.CorrelationId] = taskCompletionSource;

            try
            {
                await _mediator.Send(command, _instanceInformationProvider.Local.Id, cancellationToken);

                var result = await taskCompletionSource.WaitForCommandCompletion(CreateUpdateSystemFailedResult, cancellationToken);
                if (result is SaveSuccessResult)
                {
                    state.SwuFilename = null;
                    state.SwuFilenameUploaded = null;
                    state.SwuFileUploadTicket = null;
                    state.SwuFilenameChangedBannerVisible = false;
                }

                return result;
            }
            finally
            {
                _taskCompletionSourceMap.TryRemove(command.CorrelationId, out _);
            }
        }

        return new SaveSuccessResult();
    }

    private static ISaveResult CreateInstallOfSelectedVersionFailedResult(ErrorInfo errorInfo)
        => new SaveErrorResult($"{Localization.UpdateControlPanelSaveHandler.InstallOfSelectedVersionFailed} {CommonPhrases.SeeLogsForFurtherDetails}", errorInfo.ErrorCode);

    private static ISaveResult CreateUpdateSystemFailedResult(ErrorInfo errorInfo)
        => new SaveErrorResult($"{Localization.UpdateControlPanelSaveHandler.UpdateSystemFailed} {CommonPhrases.SeeLogsForFurtherDetails}", errorInfo.ErrorCode);

    public Task Consume(ClientContext<InstallSuiteVersionStarted> context, CancellationToken cancellationToken)
    {
        if (_taskCompletionSourceMap.TryRemove(context.Message.CorrelationId, out var installSuiteVersionTaskCompletionSource))
            installSuiteVersionTaskCompletionSource.SetResult(null);

        InstallOfSelectedVersionStarted(_logger, context.Message.Message, context.Message.WithWarnings);

        return Task.CompletedTask;
    }

    public Task Consume(ClientContext<InstallSuiteVersionError> context, CancellationToken cancellationToken)
    {
        if (_taskCompletionSourceMap.TryRemove(context.Message.CorrelationId, out var installSuiteVersionTaskCompletionSource))
            installSuiteVersionTaskCompletionSource.SetResult(context.Message.Error);

        InstallOfSelectedVersionFailed(_logger, context.Message.Error.ErrorCode, context.Message.Error.Message);

        return Task.CompletedTask;
    }

    public Task Consume(ClientContext<UpdateSystemStarted> context, CancellationToken cancellationToken)
    {
        if (_taskCompletionSourceMap.TryRemove(context.Message.CorrelationId, out var installSuiteVersionTaskCompletionSource))
            installSuiteVersionTaskCompletionSource.SetResult(null);

        UpdateSystemStarted(_logger, context.Message.Message, context.Message.WithWarnings);

        return Task.CompletedTask;
    }

    public Task Consume(ClientContext<UpdateSystemError> context, CancellationToken cancellationToken)
    {
        if (_taskCompletionSourceMap.TryRemove(context.Message.CorrelationId, out var installSuiteVersionTaskCompletionSource))
            installSuiteVersionTaskCompletionSource.SetResult(context.Message.Error);

        UpdateSystemFailed(_logger, context.Message.Error.ErrorCode, context.Message.Error.Message);

        return Task.CompletedTask;
    }

    [LoggerMessage(1, LogLevel.Error, "Install of selected version failed with error code {ErrorCode} and message {Message}")]
    private static partial void InstallOfSelectedVersionFailed(ILogger logger, int ErrorCode, string? Message);

    [LoggerMessage(2, LogLevel.Information, "Install of selected version started (with warnings = {WithWarnings}, message = {Message})")]
    private static partial void InstallOfSelectedVersionStarted(ILogger logger, string? Message, bool WithWarnings);

    [LoggerMessage(3, LogLevel.Error, "Update system failed with error code {ErrorCode} and message {Message}")]
    private static partial void UpdateSystemFailed(ILogger logger, int ErrorCode, string? Message);

    [LoggerMessage(4, LogLevel.Information, "Update system started (with warnings = {WithWarnings}, message = {Message})")]
    private static partial void UpdateSystemStarted(ILogger logger, string? Message, bool WithWarnings);
}
