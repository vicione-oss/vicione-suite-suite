using Blazor.Shared.Instance.ControlPanels.Update;
using Blazor.Shared.Instance.ControlPanels.Update.Services;
using Blazor.Shared.Instance.Extensions;
using Blazor.Shared.Services;
using Blazor.Shared.Settings.Extensions;
using Blazor.Shared.Settings.Services;
using Blazor.Tests.Tools;
using Bunit;
using Core.Shared;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Client.Components.Settings;
using Sdk.Client.Contracts;
using Sdk.Client.ControlPanels.Services;
using Sdk.Client.Infrastructure;
using Sdk.Client.Models;
using Sdk.Client.Services;
using Sdk.Instance;
using Sdk.MessageBanner.Contracts;
using ViciOne.Ui.Blazor.Components.Popup.Extensions;
using ViciOne.Ui.Localization.Resources;

namespace Blazor.Shared.Tests.Instance.ControlPanels;

public sealed class UpdateControlPanelTests
{
    private const int BackupPageIndex = 1;

    [Fact]
    public async Task Should_show_the_export_button_as_busy_while_the_backup_is_created()
    {
        // Arrange
        var mediator = Substitute.For<IUiMediator>();

        await using var ctx = new BunitContext();

        ctx.SetupBlazorSharedSettings(setup =>
        {
            setup.Services.AddControlPanelInfrastructure();
            setup.Services.AddPopup();
            setup.Services.AddUpdateControlPanel();

            setup.Services.AddSingleton(mediator);
            setup.Services.AddSingleton(Substitute.For<IActiveControlPanelPageProvider>());
            setup.Services.AddSingleton(Substitute.For<INavigationService>());
            setup.Services.AddSingleton(Substitute.For<IMessageBannerService>());
            setup.Services.AddSingleton(Substitute.For<IJsInterop>());
            setup.Services.AddSingleton(Substitute.For<IInstanceInformationProvider>());
            setup.Services.AddSingleton(Substitute.For<IUploadTicketFactory>());
            setup.Services.AddSingleton(Substitute.For<IStreamUploadHandler<ImageUpload>>());
            setup.Services.AddSingleton(Substitute.For<IStreamUploadHandler<BackupUpload>>());
        });

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
}
