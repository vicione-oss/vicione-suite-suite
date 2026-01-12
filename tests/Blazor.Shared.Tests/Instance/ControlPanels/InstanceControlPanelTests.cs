using Blazor.Shared.Instance.ControlPanels;
using Blazor.Shared.Instance.Extensions;
using Blazor.Shared.Instance.Services;
using Blazor.Shared.Services;
using Bunit;
using Core.Shared.Instance.Requests;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Sdk.Client.Infrastructure;
using Sdk.Testing.Client;
using Xunit;

namespace Blazor.Shared.Tests.Instance.ControlPanels;

public class InstanceControlPanelTests
{
    [Fact]
    public void Should_render_component()
    {
        // Arrange
        var state = new InstanceControlPanelState();
        using var ctx = new TestContext();
        ctx.SetupSuiteServices(setup =>
        {
            setup.ClientMediator.Request<GetInstances, GetInstancesResponse>(
                Arg.Any<GetInstances>(), Arg.Any<CancellationToken>())
                .Returns(new GetInstancesResponse([]));
        });
        ctx.Services.AddSingleton(Substitute.For<IBackendLogService>());

        ctx.Services.AddInstanceControlPanel();
        ctx.SetupControlPanelServices();

        // Act
        var component = ctx.RenderComponent<InstanceControlPanel>(p => p.Add(c => c.State, state));

        // Assert
        Assert.NotNull(component);
    }

    [Fact]
    public void Should_load_instance_information_if_state_has_id()
    {
        // Arrange
        var mediator = Substitute.For<IUiMediator>();
        using var ctx = new TestContext();
        ctx.SetupSuiteServices(setup =>
        {
            setup.ClientMediator.Request<GetInstances, GetInstancesResponse>(
                Arg.Any<GetInstances>(), Arg.Any<CancellationToken>())
                .Returns(new GetInstancesResponse([]));

            mediator = setup.ClientMediator;
        });
        ctx.Services.AddSingleton(Substitute.For<IBackendLogService>());

        var state = new InstanceControlPanelState() { InstanceId = Guid.NewGuid() };

        ctx.Services.AddInstanceControlPanel();
        ctx.SetupControlPanelServices();

        // Act
        var component = ctx.RenderComponent<InstanceControlPanel>(p => p.Add(c => c.State, state));

        // Assert
        Assert.NotNull(component);
        Assert.NotNull(mediator);
        mediator.Received()
            .Request<GetInstances, GetInstancesResponse>(Arg.Is<GetInstances>(k => k.InstanceId == state.InstanceId), Arg.Any<CancellationToken>());
    }
}
