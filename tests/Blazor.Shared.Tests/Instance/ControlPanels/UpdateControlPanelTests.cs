using System.Globalization;
using Blazor.Shared.Instance.ControlPanels.Update;
using Blazor.Shared.Instance.ControlPanels.Update.Services;
using Blazor.Shared.Instance.Extensions;
using Blazor.Shared.Services;
using Blazor.Shared.Settings.Extensions;
using Blazor.Shared.Settings.Services;
using Blazor.Tests.Tools;
using Bunit;
using Core.Shared;
using Core.Shared.Persistence.Events;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Client.Components.Settings;
using Sdk.Client.Contracts;
using Sdk.Client.ControlPanels.Services;
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

public sealed class UpdateControlPanelTests
{
    private const int BackupPageIndex = 1;

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

        var controlPanelState = new UpdateControlPanelState();

        var component = ctx.RenderControlPanelPage<UpdateControlPanel, UpdateControlPanelState>(controlPanelState, BackupPageIndex);

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

        var component = ctx.RenderControlPanelPage<UpdateControlPanel, UpdateControlPanelState>(new UpdateControlPanelState(), BackupPageIndex);
        var restorePrepared = new RestoreBackupPrepared(true, new ErrorInfo(errorCode, message));

        // Act
        await component.InvokeAsync(() => component.Instance.Consume(new ClientContext<RestoreBackupPrepared>(restorePrepared, Guid.NewGuid()), CancellationToken.None));

        // Assert
        bannerService.Received(1).ShowMessageBanner(MessageType.Error, expectedBannerText);
    }

    private static void SetupServices(BunitContext ctx, IUiMediator mediator, IMessageBannerService bannerService)
        => ctx.SetupBlazorSharedSettings(setup =>
        {
            setup.Services.AddControlPanelInfrastructure();
            setup.Services.AddPopup();
            setup.Services.AddUpdateControlPanel();

            setup.Services.AddSingleton(mediator);
            setup.Services.AddSingleton(Substitute.For<IActiveControlPanelPageProvider>());
            setup.Services.AddSingleton(Substitute.For<INavigationService>());
            setup.Services.AddSingleton(bannerService);
            setup.Services.AddSingleton(Substitute.For<IJsInterop>());
            setup.Services.AddSingleton(Substitute.For<IInstanceInformationProvider>());
            setup.Services.AddSingleton(Substitute.For<IUploadTicketFactory>());
            setup.Services.AddSingleton(Substitute.For<IStreamUploadHandler<ImageUpload>>());
            setup.Services.AddSingleton(Substitute.For<IStreamUploadHandler<BackupUpload>>());
        });
}
