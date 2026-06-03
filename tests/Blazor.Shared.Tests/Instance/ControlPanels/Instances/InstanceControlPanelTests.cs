using Blazor.Shared.Instance.ControlPanels.Instances;
using Blazor.Shared.Instance.ControlPanels.Instances.Services;
using Blazor.Shared.Instance.Extensions;
using Blazor.Shared.Services;
using Blazor.Shared.Settings.Extensions;
using Bunit;
using Core.Shared.Instance.Requests;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Sdk.Client.ControlPanels.Services;
using Sdk.Client.Infrastructure;
using Sdk.Testing.Client;
using Xunit;

namespace Blazor.Shared.Tests.Instance.ControlPanels.Instances;

public class InstanceControlPanelTests
{
    [Fact]
    public void Should_render_component()
    {
        // Arrange
        var state = new InstanceControlPanelState();
        using var ctx = new BunitContext();
        ctx.SetupSuiteServices(setup =>
        {
            setup.ClientMediator.Request<GetInstances, GetInstancesResponse>(
                Arg.Any<GetInstances>(), Arg.Any<CancellationToken>())
                .Returns(new GetInstancesResponse([]));
        });
        ctx.Services.AddSingleton(Substitute.For<IBackendLogService>());
        ctx.Services.AddSingleton(Substitute.For<IClientTimeProvider>());

        ctx.SetupControlPanelServices();
        ctx.Services.AddControlPanelInfrastructure();
        ctx.Services.AddInstanceControlPanel();

        // Act
        var component = ctx.Render<InstanceControlPanel>(p => p.Add(c => c.State, state));

        // Assert
        Assert.NotNull(component);
    }

    [Fact]
    public async Task Should_load_instance_information_if_state_has_id()
    {
        // Arrange
        var mediator = Substitute.For<IUiMediator>();
        await using var ctx = new BunitContext();
        ctx.SetupSuiteServices(setup =>
        {
            setup.ClientMediator.Request<GetInstances, GetInstancesResponse>(
                Arg.Any<GetInstances>(), Arg.Any<CancellationToken>())
                .Returns(new GetInstancesResponse([]));

            mediator = setup.ClientMediator;
        });
        ctx.Services.AddSingleton(Substitute.For<IBackendLogService>());
        ctx.Services.AddSingleton(Substitute.For<IClientTimeProvider>());

        var state = new InstanceControlPanelState() { InstanceId = Guid.NewGuid() };

        ctx.Services.AddControlPanelInfrastructure();
        ctx.Services.AddInstanceControlPanel();
        ctx.SetupControlPanelServices();

        var resetHandler = ctx.Services.GetRequiredService<IControlPanelResetHandler<InstanceControlPanelState>>();

        // Act
        await resetHandler.Reset(state, Xunit.TestContext.Current.CancellationToken);

        var component = ctx.Render<InstanceControlPanel>(p => p.Add(c => c.State, state));

        // Assert
        Assert.NotNull(component);
        Assert.NotNull(mediator);
        await mediator.Received().Request<GetInstances, GetInstancesResponse>(
            Arg.Is<GetInstances>(k => k.InstanceId == state.InstanceId), Arg.Any<CancellationToken>());
    }
}
