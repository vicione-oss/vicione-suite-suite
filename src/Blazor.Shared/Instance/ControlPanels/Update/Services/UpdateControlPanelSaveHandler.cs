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
using ViciOne.Ui.Localization.Resources;

namespace Blazor.Shared.Instance.ControlPanels.Update.Services;

internal sealed partial class UpdateControlPanelSaveHandler : ControlPanelSaveHandlerBase<UpdateControlPanelState>,
    IEventConsumer<InstallSuiteVersionStarted>,
    IEventConsumer<UpdateSystemStarted>
{
    private readonly IInstanceInformationProvider _instanceInformationProvider;
    private readonly IRequiredValidator _requiredValidator;
    private readonly ILogger<UpdateControlPanelSaveHandler> _logger;

    public UpdateControlPanelSaveHandler(IUiMediator mediator,
        IInstanceInformationProvider instanceInformationProvider,
        IRequiredValidator requiredValidator,
        ILogger<UpdateControlPanelSaveHandler> logger) : base(mediator)
    {
        _instanceInformationProvider = instanceInformationProvider;
        _requiredValidator = requiredValidator;
        _logger = logger;

        Register<InstallSuiteVersionStarted>();
        Register<UpdateSystemStarted>();
    }

    public override async Task<ISaveResult> Save(UpdateControlPanelState state, CancellationToken cancellationToken)
    {
        if (state.SelectedVersion is not null && state.SelectedVersionChanged && state.FlashDeviceEnabled)
            return new SaveErrorResult(Localization.UpdateControlPanelSaveHandler
                .InstallVersionAndUpdateSystemNotAllowedAtTheSameTime);

        if (state.SelectedVersion is not null && state.SelectedVersionChanged)
        {
            var installVersion = state.SuiteVersions?.FirstOrDefault(k => k.Version == state.SelectedVersion)
                                 ?? throw new InvalidOperationException(
                                     "Selected version not found in available suite versions.");

            var command = new InstallSuiteVersion(installVersion.PackageName, installVersion.SignatureName);

            var result = await SendAndWaitForCompletion(command, CreateUpdateSystemFailedResult, cancellationToken);
            if (result is SaveSuccessResult)
                state.SelectedVersionChanged = false;
        }

        if (state.FlashDeviceEnabled)
        {
            if (!_requiredValidator.Validate(state.SwuFilenameUploaded, TechnicalTerms.Image, out var errorMessage))
                return new SaveErrorResult(errorMessage);

            var command = new UpdateSystem(state.SwuFilenameUploaded);

            var result = await SendAndWaitForCompletion(
                command,
                _instanceInformationProvider.Local.Id,
                CreateInstallOfSelectedVersionFailedResult,
                cancellationToken);

            if (result is SaveSuccessResult)
            {
                state.SwuFilename = null;
                state.SwuFilenameUploaded = null;
                state.SwuFileUploadTicket = null;
                state.SwuFilenameChangedBannerVisible = false;
            }

            return result;
        }

        return new SaveSuccessResult();
    }

    private static ISaveResult CreateInstallOfSelectedVersionFailedResult(ErrorInfo errorInfo)
        => new SaveErrorResult(
            $"{Localization.UpdateControlPanelSaveHandler.InstallOfSelectedVersionFailed} {CommonPhrases.SeeLogsForFurtherDetails}",
            errorInfo.ErrorCode);

    private static ISaveResult CreateUpdateSystemFailedResult(ErrorInfo errorInfo)
        => new SaveErrorResult(
            $"{Localization.UpdateControlPanelSaveHandler.UpdateSystemFailed} {CommonPhrases.SeeLogsForFurtherDetails}",
            errorInfo.ErrorCode);

    public Task Consume(ClientContext<InstallSuiteVersionStarted> context, CancellationToken cancellationToken)
    {
        if (context.Message.ErrorInfo is not null)
        {
            InstallOfSelectedVersionFailed(_logger, context.Message.ErrorInfo.ErrorCode, context.Message.ErrorInfo.Message);

            CompleteWithError(context.Message.CorrelationId, context.Message.ErrorInfo);
            return Task.CompletedTask;
        }

        CompleteWithSuccess(context.Message.CorrelationId);

        InstallOfSelectedVersionStarted(_logger, context.Message.Message, context.Message.WithWarnings);

        return Task.CompletedTask;
    }

    public Task Consume(ClientContext<UpdateSystemStarted> context, CancellationToken cancellationToken)
    {
        if (context.Message.ErrorInfo is not null)
        {
            UpdateSystemFailed(_logger, context.Message.ErrorInfo.ErrorCode, context.Message.ErrorInfo.Message);

            CompleteWithError(context.Message.CorrelationId, context.Message.ErrorInfo);
            return Task.CompletedTask;
        }

        CompleteWithSuccess(context.Message.CorrelationId);

        UpdateSystemStarted(_logger, context.Message.Message, context.Message.WithWarnings);

        return Task.CompletedTask;
    }

    [LoggerMessage(LogLevel.Error, "Install of selected version failed with error code {ErrorCode} and message {Message}")]
    private static partial void InstallOfSelectedVersionFailed(ILogger logger, int ErrorCode, string? Message);

    [LoggerMessage(LogLevel.Information, "Install of selected version started (with warnings = {WithWarnings}, message = {Message})")]
    private static partial void InstallOfSelectedVersionStarted(ILogger logger, string? Message, bool WithWarnings);

    [LoggerMessage(LogLevel.Error, "Update system failed with error code {ErrorCode} and message {Message}")]
    private static partial void UpdateSystemFailed(ILogger logger, int ErrorCode, string? Message);

    [LoggerMessage(LogLevel.Information, "Update system started (with warnings = {WithWarnings}, message = {Message})")]
    private static partial void UpdateSystemStarted(ILogger logger, string? Message, bool WithWarnings);
}
