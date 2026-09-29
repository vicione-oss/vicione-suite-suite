using Blazor.Shared.Instance.ControlPanels.Update.Services;
using Blazor.Shared.Services;
using Core.Shared;
using Core.Shared.HostManagement.Commands;
using Core.Shared.HostManagement.Events;
using Core.Shared.Persistence.Commands;
using Core.Shared.Persistence.Events;
using Core.Shared.Persistence.Requests;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Sdk.Authorization;
using Sdk.Client.Contracts;
using Sdk.Client.ControlPanels.Attributes;
using Sdk.Client.ControlPanels.Components;
using Sdk.Client.Infrastructure;
using Sdk.Client.Models;
using Sdk.Client.Services;
using Sdk.Instance;
using Sdk.MessageBanner.Contracts;
using Sdk.Utils;
using ViciOne.Ui.MonochromeIcons.Core.Enums;
using ViciOne.Ui.MonochromeIcons.Core.Extensions;

namespace Blazor.Shared.Instance.ControlPanels.Update;

[ControlPanelCategory<ControlPanelUpdateAndRestoreCategoryDescriptor>]
[ModuleAuthorize(SharedClientModule.ModuleId, AccessLevel.Full)]
public sealed partial class UpdateControlPanel : ControlPanelBase<UpdateControlPanelState>,
    IEventConsumer<RestoreBackupPrepared>,
    IEventConsumer<BackupFinished>,
    IEventConsumer<ControlSystemCompleted>
{
    private readonly string _closeIconCssClass = MonochromeIconName.CloseMedium.GetCssClasses(MonochromeIconSize.Small).ToSpaceSeparated();
    private readonly string _exportIconCssClasses = MonochromeIconName.Export.GetCssClasses(MonochromeIconSize.Small).ToSpaceSeparated();
    private readonly string _refreshButtonIconCssClass = MonochromeIconName.Refresh.GetCssClasses(MonochromeIconSize.Small).ToSpaceSeparated();
    private readonly string _refreshIconCssClass = MonochromeIconName.Refresh.GetCssClasses().ToSpaceSeparated();
    private readonly string _saveIconCssClasses = MonochromeIconName.SaveOutline.GetCssClasses().ToSpaceSeparated();

    private readonly AutoDisposeList<IDisposable> _subscriptionHandle = [];

    private bool _confirmation;
    private bool _showResetConfirmSection;
    private bool _showErrorDialog;
    private bool _isExportInProgress;
    private bool _isResetInProgress;
    private bool _isRestoreInProgress;
    private string? _errorMessage;
    private Guid? _exportCorrelationId;
    private Guid? _resetCorrelationId;

    [Inject]
    public IUiMediator Mediator { get; set; } = default!;

    [Inject]
    public IInstanceInformationProvider InstanceInformation { get; set; } = default!;

    [Inject]
    public ILogger<UpdateControlPanel> Logger { get; set; } = default!;

    [Inject]
    public IMessageBannerService BannerService { get; set; } = default!;

    [Inject]
    public IJsInterop JsInterop { get; set; } = default!;

    [Inject]
    private INavigationService NavigationManager { get; set; } = default!;

    [Inject]
    private IStreamUploadHandler<ImageUpload> StreamUploadHandler { get; set; } = default!;

    [Inject]
    private IStreamUploadHandler<BackupUpload> BackupUploadHandler { get; set; } = default!;

    [Inject]
    private IUploadTicketFactory UploadTicketFactory{ get; set; } = default!;

    protected override void OnInitialized()
    {
        _subscriptionHandle.Add(Mediator.Register<RestoreBackupPrepared>(this));
        _subscriptionHandle.Add(Mediator.Register<BackupFinished>(this));

        _subscriptionHandle.Add(Mediator.Register<ControlSystemCompleted>(this));

        base.OnInitialized();
    }

    protected override async ValueTask DisposeAsyncCore()
    {
        _subscriptionHandle.Dispose();

        await base.DisposeAsyncCore();
    }

    private bool CanExecuteExportButton()
        => !_isRestoreInProgress;

    private void RestoreFileUploadStart(IUploadTicket uploadTicket)
    {
        _errorMessage = null;
        State.RestoreFileUploadTicket = uploadTicket;
    }

    private async Task RestoreFileUploadSuccess(StreamUploadSuccessResult successResult)
    {
        try
        {
            _isRestoreInProgress = true;

            // TODO: checkboxes for System/Suite configuration
            var command = new RestoreBackup
            {
                BackupFilePath = successResult.DestinationFile,
                SuiteConfiguration = true,
                SystemConfiguration = true
            };

            await Mediator.Send(command);
        }
        catch (Exception ex)
        {
            _errorMessage = ex.Message;
            _isRestoreInProgress = false;
            Logger.LogError(ex, "Failed to restore backup");
        }
    }

    private void RestoreFileUploadError(StreamUploadErrorResult errorResult)
    {
        _isRestoreInProgress = false;
        State.RestoreFilename = null;
        BannerService.ShowMessageBanner(MessageType.Error, errorResult.Message);
    }

    private void RestoreFileUploadCancel()
    {
        _isRestoreInProgress = false;
        State.RestoreFilename = null;
    }

    private void OnResetButtonClick()
    {
        _errorMessage = null;
        _isResetInProgress = false;
        _showResetConfirmSection = true;
    }

    private async Task OnResetConfirmedButtonClick()
    {
        try
        {
            _errorMessage = null;
            _isResetInProgress = true;
            var command = new ControlSystem(SystemCommand.Reset);
            _resetCorrelationId = command.CorrelationId;
            await Mediator.Send(command, InstanceInformation.Local.Id);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to control system");
            _errorMessage = ex.Message;
            _isResetInProgress = false;
            _resetCorrelationId = null;
        }
    }

    private void OnNoButtonClick()
    {
        _confirmation = false;
        _isResetInProgress = false;
        _showResetConfirmSection = false;
    }

    private async Task OnExportButtonClick()
    {
        try
        {
            // Goes away once backups run on an interval.
            // and downloaded directly from grid!
            // todo: allow backup file selection instead only allow loading last one
            _errorMessage = null;
            _isExportInProgress = true;

            // A command creates the backup; the correlating backup-created event carries the result.
            // we'll trigger request this backup and start the download
            var command = new CreateBackup();
            _exportCorrelationId = command.CorrelationId;
            await Mediator.Send(command);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to restore backup");
            _errorMessage = ex.Message;
            _exportCorrelationId = null;
            _isExportInProgress = false;
        }
    }

    public Task Consume(ClientContext<RestoreBackupPrepared> context, CancellationToken cancellationToken)
    {
        // The suite restarts shortly after this message arrives.
        // Not correlated, because it is relevant for every user.
        _isRestoreInProgress = false;

        if (context.Message.ErrorInfo is not null)
        {
            BannerService.ShowMessageBanner(MessageType.Error, context.Message.ErrorInfo.Message);
        }
        else
            BannerService.ShowMessageBanner(MessageType.Information, Localization.UpdateControlPanel.BackupRestoreInProgress);

        return Task.CompletedTask;
        // Once the restore is prepared, a ControlSystemCompleted arrives if a restart was triggered.
    }

    public async Task Consume(ClientContext<ControlSystemCompleted> context, CancellationToken cancellationToken)
    {
        if (context.Message.ErrorInfo is not null)
        {
            if (context.Message.CorrelationId != _resetCorrelationId)
                return;

            _errorMessage = context.Message.ErrorInfo.Message;
            _isResetInProgress = false;
            _resetCorrelationId = null;
            BannerService.ShowMessageBanner(MessageType.Information, _errorMessage);
            return;
        }

        // Not correlated, because it is relevant for every user.
        _errorMessage = null;
        _isResetInProgress = false;
        BannerService.ShowMessageBanner(MessageType.Information, Localization.UpdateControlPanel.ResetInProgressMessage);

        // No delay here because if we receive it restart will come immediately so user can't probably read the banner
        await NavigationManager.Logout();
    }

    public async Task Consume(ClientContext<BackupFinished> context, CancellationToken cancellationToken)
    {
        if (context.CorrelationId != _exportCorrelationId)
            return;

        if (context.Message.ErrorInfo is not null)
        {
            _exportCorrelationId = null;
            _isExportInProgress = false;
            BannerService.ShowMessageBanner(MessageType.Error, context.Message.ErrorInfo.Message);
            return;
        }

        try
        {
            var request = new GetBackup();
            var response = await Mediator.Request<GetBackup, GetBackupResponse>(request, cancellationToken);

            if (response.RequestError is not null)
                throw new InvalidOperationException(response.RequestError.Message);

            if (response.Content is null)
                throw new InvalidOperationException($"Content of {response.FileName} is null");

            using var memoryStream = new MemoryStream(response.Content);

            await JsInterop.DownloadAs(memoryStream, response.FileName, cancellationToken);
        }
        catch (Exception e)
        {
            BannerService.ShowMessageBanner(MessageType.Error, e.Message);
        }
        finally
        {
            _exportCorrelationId = null;
            _isExportInProgress = false;

            await InvokeAsync(StateHasChanged);
        }
    }

    private void SwuFileUploadStart(IUploadTicket uploadTicket)
    {
        State.SwuUploadMessage = null;
        State.SwuFileUploadTicket = uploadTicket;
        State.SwuFilenameUploaded = null;
    }

    private async Task SwuFileUploadSuccess(StreamUploadSuccessResult successResult)
    {
        State.SwuFilenameUploaded = successResult.DestinationFile;
        State.SwuFilenameChangedBannerVisible = true;

        await BeginEdit();
    }

    private void SwuFileUploadError(StreamUploadErrorResult errorResult)
        => ResetSwuUpload(errorResult.Message);

    private void SwuFileUploadCancel()
        => ResetSwuUpload(Localization.UpdateControlPanel.FileUploadCancelled);

    private void ResetSwuUpload(string? message = null)
    {
        State.SwuUploadMessage = message;
        State.SwuFilename = null;
        State.SwuFilenameUploaded = null;
        State.SwuFilenameChangedBannerVisible = false;
    }

    private async Task SelectedVersionChanged()
    {
        var installed = State.SuiteVersions?.Single(k => k.Installed);
        State.SelectedVersionChanged = !string.Equals(installed?.Version, State.SelectedVersion, StringComparison.Ordinal);

        await BeginEdit();
    }
}
