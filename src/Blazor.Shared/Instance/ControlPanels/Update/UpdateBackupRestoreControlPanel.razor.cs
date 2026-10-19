using System.Globalization;
using Blazor.Shared.Instance.ControlPanels.Update.Services;
using Core.Shared;
using Microsoft.AspNetCore.Components;
using Sdk.Authorization;
using Sdk.Client.Contracts;
using Sdk.Client.ControlPanels.Attributes;
using Sdk.Client.ControlPanels.Components;
using Sdk.Client.Models;
using Sdk.Client.Services;
using Sdk.MessageBanner.Contracts;
using Sdk.Utils;
using ViciOne.Ui.MonochromeIcons.Core.Enums;
using ViciOne.Ui.MonochromeIcons.Core.Extensions;

namespace Blazor.Shared.Instance.ControlPanels.Update;

[ControlPanelCategory<ControlPanelUpdateAndRestoreCategoryDescriptor>]
[ControlPanelChild<UpdateControlPanelDescriptor>]
[ModuleAuthorize(SharedClientModule.ModuleId, AccessLevel.Full)]
public sealed partial class UpdateBackupRestoreControlPanel : ControlPanelBase<UpdateBackupRestoreControlPanelState>
{
    private readonly string _exportIconCssClasses = MonochromeIconName.Export.GetCssClasses(MonochromeIconSize.Small).ToSpaceSeparated();
    private readonly string _saveIconCssClasses = MonochromeIconName.SaveOutline.GetCssClasses().ToSpaceSeparated();

    [Inject]
    public IMessageBannerService BannerService { get; set; } = default!;

    [Inject]
    private UpdateBackupRestoreService BackupRestoreService { get; set; } = default!;

    [Inject]
    private IStreamUploadHandler<BackupUpload> BackupUploadHandler { get; set; } = default!;

    [Inject]
    private IUploadTicketFactory UploadTicketFactory { get; set; } = default!;

    protected override void OnInitialized()
    {
        base.OnInitialized();

        BackupRestoreService.Changed += OnBackupRestoreServiceChanged;
    }

    protected override async ValueTask DisposeAsyncCore()
    {
        BackupRestoreService.Changed -= OnBackupRestoreServiceChanged;

        // Leaving the tab disposes the upload field, which ends the upload, so the user is told instead of finding it gone.
        if (State.RestoreUploadRunning)
        {
            var filename = State.RestoreFilename;

            State.RestoreFileUploadTicket?.Cancel();
            State.RestoreFileUploadTicket = null;
            State.RestoreFilename = null;
            State.RestoreUploadRunning = false;

            BannerService.ShowMessageBanner(MessageType.Warning,
                string.Format(CultureInfo.CurrentCulture, Localization.UpdateControlPanel.FileUploadCancelledOnLeave, filename));
        }

        await base.DisposeAsyncCore();
    }

    private void OnBackupRestoreServiceChanged()
        => _ = InvokeAsync(StateHasChanged);

    private bool CanExecuteExportButton()
        => !BackupRestoreService.IsRestoreInProgress;

    private void RestoreFileUploadStart(IUploadTicket uploadTicket)
    {
        BackupRestoreService.ClearError();
        State.RestoreFileUploadTicket = uploadTicket;
        State.RestoreUploadRunning = true;
    }

    private Task RestoreFileUploadSuccess(StreamUploadSuccessResult successResult)
    {
        State.RestoreUploadRunning = false;

        return BackupRestoreService.StartRestore(successResult.DestinationFile);
    }

    private void RestoreFileUploadError(StreamUploadErrorResult errorResult)
    {
        BackupRestoreService.EndRestore();
        State.RestoreUploadRunning = false;
        State.RestoreFilename = null;
        BannerService.ShowMessageBanner(MessageType.Error, errorResult.Message);
    }

    private void RestoreFileUploadCancel()
    {
        BackupRestoreService.EndRestore();
        State.RestoreUploadRunning = false;
        State.RestoreFilename = null;
    }

    private Task OnExportButtonClick()
        => BackupRestoreService.StartExport();
}
