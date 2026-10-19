using System.Globalization;
using Blazor.Shared.Instance.ControlPanels.Update;
using Blazor.Shared.Instance.ControlPanels.Update.Services;
using Blazor.Shared.Instance.Extensions;
using Blazor.Shared.Services;
using Blazor.Shared.Settings.Extensions;
using Blazor.Tests.Tools;
using Bunit;
using Core.Shared;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Client.Contracts;
using Sdk.Client.ControlPanels.Services;
using Sdk.Client.Infrastructure;
using Sdk.Client.Models;
using Sdk.Client.Services;
using Sdk.Instance;
using Sdk.MessageBanner.Contracts;
using ViciOne.Ui.Blazor.Components.Popup.Extensions;
using UpdateControlPanelTexts = Blazor.Shared.Instance.ControlPanels.Update.Localization.UpdateControlPanel;

namespace Blazor.Shared.Tests.Instance.ControlPanels;

public sealed class UpdateControlPanelTests
{
    [Fact]
    public async Task Should_cancel_the_device_image_upload_and_warn_when_the_tab_is_left_during_the_upload()
    {
        // Arrange
        var bannerService = Substitute.For<IMessageBannerService>();
        var uploadTicket = Substitute.For<IUploadTicket>();
        var controlPanelState = new UpdateControlPanelState
        {
            FlashDeviceEnabled = true,
            SwuFilename = "image.swu",
            SwuFileUploadTicket = uploadTicket,
            SwuUploadRunning = true
        };

        var ctx = new BunitContext();
        ctx.SetupBlazorSharedSettings(setup =>
        {
            setup.Services.AddControlPanelInfrastructure();
            setup.Services.AddPopup();
            setup.Services.AddUpdateControlPanel();

            setup.Services.AddSingleton(Substitute.For<IUiMediator>());
            setup.Services.AddSingleton(Substitute.For<INavigationService>());
            setup.Services.AddSingleton(bannerService);
            setup.Services.AddSingleton(Substitute.For<IJsInterop>());
            setup.Services.AddSingleton(Substitute.For<IInstanceInformationProvider>());
            setup.Services.AddSingleton(Substitute.For<IUploadTicketFactory>());
            setup.Services.AddSingleton(Substitute.For<IStreamUploadHandler<ImageUpload>>());
            setup.Services.AddSingleton(Substitute.For<IStreamUploadHandler<BackupUpload>>());
        });

        ctx.Render<UpdateControlPanel>(builder => builder.Add(p => p.State, controlPanelState));

        // Act
        await ctx.DisposeAsync();

        // Assert
        uploadTicket.Received(1).Cancel();
        bannerService.Received(1).ShowMessageBanner(MessageType.Warning,
            string.Format(CultureInfo.CurrentCulture, UpdateControlPanelTexts.FileUploadCancelledOnLeave, "image.swu"));
        controlPanelState.SwuFilename.Should().BeNull();
        controlPanelState.SwuUploadRunning.Should().BeFalse();
        controlPanelState.SwuUploadMessage.Should().Be(UpdateControlPanelTexts.FileUploadCancelled);
    }
}
