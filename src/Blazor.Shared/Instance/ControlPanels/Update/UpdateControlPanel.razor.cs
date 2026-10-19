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
using ViciOne.Ui.MonochromeIcons.Core.Enums;
using ViciOne.Ui.MonochromeIcons.Core.Extensions;

namespace Blazor.Shared.Instance.ControlPanels.Update;

[ControlPanelCategory<ControlPanelUpdateAndRestoreCategoryDescriptor>]
[ControlPanelParent<UpdateControlPanelDescriptor>]
[ModuleAuthorize(SharedClientModule.ModuleId, AccessLevel.Full)]
public sealed partial class UpdateControlPanel : ControlPanelBase<UpdateControlPanelState>
{
    private readonly string _refreshIconCssClass = MonochromeIconName.Refresh.GetCssClasses().ToSpaceSeparated();

    [Inject]
    public IMessageBannerService BannerService { get; set; } = default!;

    [Inject]
    private IStreamUploadHandler<ImageUpload> StreamUploadHandler { get; set; } = default!;

    [Inject]
    private IUploadTicketFactory UploadTicketFactory { get; set; } = default!;

    protected override async ValueTask DisposeAsyncCore()
    {
        // Leaving the tab disposes the upload field, which ends the upload, so the user is told instead of finding it gone.
        if (State.SwuUploadRunning)
        {
            var filename = State.SwuFilename;

            State.SwuFileUploadTicket?.Cancel();
            State.SwuFileUploadTicket = null;
            ResetSwuUpload(Localization.UpdateControlPanel.FileUploadCancelled);

            BannerService.ShowMessageBanner(MessageType.Warning,
                string.Format(CultureInfo.CurrentCulture, Localization.UpdateControlPanel.FileUploadCancelledOnLeave, filename));
        }

        await base.DisposeAsyncCore();
    }

    private void SwuFileUploadStart(IUploadTicket uploadTicket)
    {
        State.SwuUploadMessage = null;
        State.SwuFileUploadTicket = uploadTicket;
        State.SwuUploadRunning = true;
        State.SwuFilenameUploaded = null;
    }

    private async Task SwuFileUploadSuccess(StreamUploadSuccessResult successResult)
    {
        State.SwuUploadRunning = false;
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
        State.SwuUploadRunning = false;
        State.SwuUploadMessage = message;
        State.SwuFilename = null;
        State.SwuFilenameUploaded = null;
        State.SwuFilenameChangedBannerVisible = false;
    }

    private async Task SelectedVersionChanged()
    {
        var installed = State.SuiteVersions?.Single(k => k.Installed);
        State.SelectedVersionChanged = !string.Equals(installed?.Version, State.SelectedVersion, StringComparison.Ordinal);

        if (State.SelectedVersionChanged)
        {
            await BeginEdit();
            return;
        }

        // Cancelling resets the whole panel, so it would also discard an uploaded device image.
        if (State.SwuFilenameUploaded is null)
            await CancelEdit();
    }
}
