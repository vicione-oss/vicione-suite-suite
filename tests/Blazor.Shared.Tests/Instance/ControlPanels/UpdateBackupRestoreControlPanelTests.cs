using System.Globalization;
using Blazor.Shared.Instance.ControlPanels.Update;
using Blazor.Shared.Instance.ControlPanels.Update.Services;
using Blazor.Shared.Instance.Extensions;
using Blazor.Shared.Services;
using Blazor.Shared.Settings.Extensions;
using Bunit;
using Core.Shared;
using Core.Shared.Persistence.Commands;
using Core.Shared.Persistence.Events;
using Core.Shared.Persistence.Requests;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Client.Components.Settings;
using Sdk.Client.Contracts;
using Sdk.Client.Infrastructure;
using Sdk.Client.Models;
using Sdk.Client.Services;
using Sdk.Instance;
using Sdk.MessageBanner.Contracts;
using Sdk.Messaging;
using ViciOne.Ui.Blazor.Components.Popup.Extensions;
using ViciOne.Ui.Localization.Resources;
using CapabilityTexts = Blazor.Shared.Localization.HostManagementCapabilities;

namespace Blazor.Shared.Tests.Instance.ControlPanels;

public sealed class UpdateBackupRestoreControlPanelTests
{
    public static TheoryData<int, string, string> RestoreErrors => new()
    {
        { RestoreBackupPrepared.RestartServiceDisabled, "The capability 'RestartService' is disabled in HostManagement.", CapabilityTexts.FunctionDisabled },
        { RestoreBackupPrepared.SettingsDisabled, "Hostname, IPv4", string.Format(CultureInfo.CurrentCulture, CapabilityTexts.SettingsDisabled, "Hostname, IPv4") },
        { RestoreBackupPrepared.BackupFormatNotSupported, "The system configuration in this backup has a format that is no longer supported.", Blazor.Shared.Instance.ControlPanels.Update.Localization.UpdateControlPanel.BackupFormatNotSupported },
        { 100, "Restore backup of another instances is not supported.", "Restore backup of another instances is not supported." },
    };

    [Fact]
    public async Task Should_show_the_export_button_as_busy_while_the_backup_is_created()
    {
        // Arrange
        var mediator = Substitute.For<IUiMediator>();

        await using var ctx = new BunitContext();
        SetupServices(ctx, mediator, Substitute.For<IMessageBannerService>());

        var controlPanelState = new UpdateBackupRestoreControlPanelState();

        var component = ctx.Render<UpdateBackupRestoreControlPanel>(builder => builder.Add(p => p.State, controlPanelState));

        var exportButton = component.FindComponents<SettingsFieldButton>()
            .Single(button => button.Instance.Text == CommonVocabulary.ExportVerb);

        // Act
        await exportButton.Find("button").ClickAsync();

        // Assert
        component.WaitForAssertion(() =>
            component.FindComponents<SettingsFieldButton>()
                .Where(button => button.Instance.Busy)
                .Should().ContainSingle()
                .Which.Instance.Text.Should().Be(CommonVocabulary.ExportVerb));
    }

    [Theory]
    [MemberData(nameof(RestoreErrors))]
    public async Task Should_show_the_restore_error(int errorCode, string message, string expectedBannerText)
    {
        // Arrange
        var bannerService = Substitute.For<IMessageBannerService>();

        await using var ctx = new BunitContext();
        SetupServices(ctx, Substitute.For<IUiMediator>(), bannerService);

        var backupRestoreService = ctx.Services.GetRequiredService<UpdateBackupRestoreService>();
        var restorePrepared = new RestoreBackupPrepared(true, new ErrorInfo(errorCode, message));

        // Act
        await backupRestoreService.Consume(new ClientContext<RestoreBackupPrepared>(restorePrepared, Guid.NewGuid()), CancellationToken.None);

        // Assert
        bannerService.Received(1).ShowMessageBanner(MessageType.Error, expectedBannerText);
    }

    [Fact]
    public async Task Should_show_the_export_button_as_busy_when_returning_to_the_tab_during_the_export()
    {
        // Arrange
        await using var ctx = new BunitContext();
        SetupServices(ctx, Substitute.For<IUiMediator>(), Substitute.For<IMessageBannerService>());

        await ctx.Services.GetRequiredService<UpdateBackupRestoreService>().StartExport();

        // Act
        var component = ctx.Render<UpdateBackupRestoreControlPanel>(builder => builder.Add(p => p.State, new UpdateBackupRestoreControlPanelState()));

        // Assert
        component.FindComponents<SettingsFieldButton>()
            .Single(button => button.Instance.Text == CommonVocabulary.ExportVerb)
            .Instance.Busy.Should().BeTrue();
    }

    [Fact]
    public async Task Should_download_the_backup_when_it_finishes_without_the_tab_being_shown()
    {
        // Arrange
        var mediator = Substitute.For<IUiMediator>();
        mediator.Request<GetBackup, GetBackupResponse>(Arg.Any<GetBackup>(), Arg.Any<CancellationToken>())
            .Returns(new GetBackupResponse("backup.v1b", [1, 2, 3]));

        var jsInterop = Substitute.For<IJsInterop>();

        await using var ctx = new BunitContext();
        SetupServices(ctx, mediator, Substitute.For<IMessageBannerService>(), jsInterop);

        var backupRestoreService = ctx.Services.GetRequiredService<UpdateBackupRestoreService>();
        await backupRestoreService.StartExport();

        var createBackup = (CreateBackup)mediator.ReceivedCalls().Single(call => call.GetArguments()[0] is CreateBackup).GetArguments()[0]!;
        var backupFinished = new BackupFinished(createBackup.CorrelationId, "backup.v1b");

        // Act
        await backupRestoreService.Consume(new ClientContext<BackupFinished>(backupFinished, createBackup.CorrelationId), CancellationToken.None);

        // Assert
        await jsInterop.Received(1).DownloadAs(Arg.Any<Stream>(), "backup.v1b", Arg.Any<CancellationToken>());
        backupRestoreService.IsExportInProgress.Should().BeFalse();
    }

    [Fact]
    public async Task Should_cancel_the_restore_upload_and_warn_when_the_tab_is_left_during_the_upload()
    {
        // Arrange
        var bannerService = Substitute.For<IMessageBannerService>();
        var uploadTicket = Substitute.For<IUploadTicket>();
        var controlPanelState = new UpdateBackupRestoreControlPanelState
        {
            RestoreFilename = "backup.v1b",
            RestoreFileUploadTicket = uploadTicket,
            RestoreUploadRunning = true
        };

        var ctx = new BunitContext();
        SetupServices(ctx, Substitute.For<IUiMediator>(), bannerService);

        ctx.Render<UpdateBackupRestoreControlPanel>(builder => builder.Add(p => p.State, controlPanelState));

        // Act
        await ctx.DisposeAsync();

        // Assert
        uploadTicket.Received(1).Cancel();
        bannerService.Received(1).ShowMessageBanner(MessageType.Warning, string.Format(CultureInfo.CurrentCulture,
            Blazor.Shared.Instance.ControlPanels.Update.Localization.UpdateControlPanel.FileUploadCancelledOnLeave, "backup.v1b"));
        controlPanelState.RestoreFilename.Should().BeNull();
        controlPanelState.RestoreUploadRunning.Should().BeFalse();
    }

    private static void SetupServices(BunitContext ctx, IUiMediator mediator, IMessageBannerService bannerService,
        IJsInterop? jsInterop = null)
        => ctx.SetupBlazorSharedSettings(setup =>
        {
            setup.Services.AddControlPanelInfrastructure();
            setup.Services.AddPopup();
            setup.Services.AddUpdateBackupRestoreControlPanel();

            setup.Services.AddSingleton(mediator);
            setup.Services.AddSingleton(Substitute.For<INavigationService>());
            setup.Services.AddSingleton(bannerService);
            setup.Services.AddSingleton(jsInterop ?? Substitute.For<IJsInterop>());
            setup.Services.AddSingleton(Substitute.For<IInstanceInformationProvider>());
            setup.Services.AddSingleton(Substitute.For<IUploadTicketFactory>());
            setup.Services.AddSingleton(Substitute.For<IStreamUploadHandler<ImageUpload>>());
            setup.Services.AddSingleton(Substitute.For<IStreamUploadHandler<BackupUpload>>());
        });
}
