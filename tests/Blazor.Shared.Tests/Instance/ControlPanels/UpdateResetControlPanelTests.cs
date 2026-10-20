using Blazor.Shared.Instance.ControlPanels.Update;
using Blazor.Shared.Instance.ControlPanels.Update.Services;
using Blazor.Shared.Instance.Extensions;
using Blazor.Shared.Services;
using Blazor.Shared.Settings.Components;
using Blazor.Shared.Settings.Extensions;
using Blazor.Tests.Tools;
using Bunit;
using Core.Shared;
using Core.Shared.HostManagement.Commands;
using Core.Shared.HostManagement.Events;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Client.Contracts;
using Sdk.Client.ControlPanels.Services;
using Sdk.Client.Infrastructure;
using Sdk.Client.Models;
using Sdk.Client.Services;
using Sdk.Instance;
using ViciOne.Ui.Blazor.Components.Popup.Extensions;

namespace Blazor.Shared.Tests.Instance.ControlPanels;

public sealed class UpdateResetControlPanelTests
{
    private readonly IUiMediator _mediator = Substitute.For<IUiMediator>();
    private readonly INavigationService _navigationService = Substitute.For<INavigationService>();

    [Fact]
    public async Task Should_show_the_reset_as_busy_when_returning_to_the_tab_during_the_reset()
    {
        // Arrange
        await using var ctx = new BunitContext();
        SetupServices(ctx);

        await ctx.Services.GetRequiredService<UpdateResetService>().StartReset();

        // Act
        var component = ctx.Render<UpdateResetControlPanel>(builder => builder.Add(p => p.State, new UpdateResetControlPanelState()));

        // Assert
        component.FindComponents<SettingsFieldButtonRed>()
            .Should().ContainSingle(button => button.Instance.Busy);
    }

    [Fact]
    public async Task Should_log_out_once_when_the_own_reset_completes_while_the_tab_is_shown()
    {
        // Arrange
        await using var ctx = new BunitContext();
        SetupServices(ctx);

        var resetService = ctx.Services.GetRequiredService<UpdateResetService>();
        var component = ctx.Render<UpdateResetControlPanel>(builder => builder.Add(p => p.State, new UpdateResetControlPanelState()));

        await resetService.StartReset();

        var controlSystem = GetSentControlSystem();
        var completed = new ControlSystemCompleted(SystemCommand.Reset) { CorrelationId = controlSystem.CorrelationId };
        var context = new ClientContext<ControlSystemCompleted>(completed, controlSystem.CorrelationId);

        // Act
        await resetService.Consume(context, CancellationToken.None);
        await component.InvokeAsync(() => component.Instance.Consume(context, CancellationToken.None));

        // Assert
        await _navigationService.Received(1).Logout();
    }

    [Fact]
    public async Task Should_log_out_when_the_reset_of_another_user_completes_while_the_tab_is_shown()
    {
        // Arrange
        await using var ctx = new BunitContext();
        SetupServices(ctx);

        var component = ctx.Render<UpdateResetControlPanel>(builder => builder.Add(p => p.State, new UpdateResetControlPanelState()));

        var correlationId = Guid.NewGuid();
        var completed = new ControlSystemCompleted(SystemCommand.Reset) { CorrelationId = correlationId };

        // Act
        await component.InvokeAsync(() => component.Instance.Consume(
            new ClientContext<ControlSystemCompleted>(completed, correlationId), CancellationToken.None));

        // Assert
        await _navigationService.Received(1).Logout();
    }

    private ControlSystem GetSentControlSystem()
        => (ControlSystem)_mediator.ReceivedCalls().Single(call => call.GetArguments()[0] is ControlSystem).GetArguments()[0]!;

    private void SetupServices(BunitContext ctx)
    {
        var instanceInformationProvider = Substitute.For<IInstanceInformationProvider>();
        var localInstance = Substitute.For<IInstanceInformation>();
        localInstance.Id.Returns(Guid.NewGuid());
        instanceInformationProvider.Local.Returns(localInstance);

        ctx.SetupBlazorSharedSettings(setup =>
        {
            setup.Services.AddControlPanelInfrastructure();
            setup.Services.AddPopup();
            setup.Services.AddUpdateResetControlPanel();

            setup.Services.AddSingleton(_mediator);
            setup.Services.AddSingleton(_navigationService);
            setup.Services.AddSingleton(Substitute.For<IMessageBannerService>());
            setup.Services.AddSingleton(Substitute.For<IJsInterop>());
            setup.Services.AddSingleton(instanceInformationProvider);
            setup.Services.AddSingleton(Substitute.For<IUploadTicketFactory>());
            setup.Services.AddSingleton(Substitute.For<IStreamUploadHandler<ImageUpload>>());
            setup.Services.AddSingleton(Substitute.For<IStreamUploadHandler<BackupUpload>>());
        });
    }
}
