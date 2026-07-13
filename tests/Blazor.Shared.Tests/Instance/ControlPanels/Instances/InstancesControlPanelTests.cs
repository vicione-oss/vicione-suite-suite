using AwesomeAssertions;
using Blazor.Shared.Instance.ControlPanels.Instances;
using Blazor.Shared.Instance.ControlPanels.Instances.Services;
using Blazor.Shared.Instance.Extensions;
using Blazor.Shared.Settings.Extensions;
using Blazor.Tests.Tools;
using Bunit;
using Core.Shared.Instance.Commands;
using Core.Shared.Instance.Contracts;
using Core.Shared.Instance.Events;
using Core.Shared.Instance.Requests;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Sdk.Client.ControlPanels.Services;
using Sdk.Client.Infrastructure;
using Sdk.Instance;
using Sdk.Testing.Client;
using ViciOne.Ui.MonochromeIcons.Core.Enums;
using Xunit;
using InstanceControlPanel = Blazor.Shared.Instance.ControlPanels.Instances.InstanceControlPanel;
using TestContext = Xunit.TestContext;

namespace Blazor.Shared.Tests.Instance.ControlPanels.Instances;

public class InstancesControlPanelTests
{
    private readonly IClusterInformationProvider _informationProvider = Substitute.For<IClusterInformationProvider>();
    private static InstanceInformation CreateSlaveInstanceInfo()
    {
        var guid = Guid.NewGuid();

        return new InstanceInformation()
        {
            Id = guid,
            FirstTimeRegistered = DateTimeOffset.Now,
            LastRegistered = DateTimeOffset.Now,
            Name = "TestInstance" + guid.ToString(),
            Type = InstanceType.Slave,
        };
    }

    private BunitContext SetupTestContext(List<InstanceInformation>? instanceInformations = null, Action<ClientServiceConfigurator>? configure = null, Action<IControlPanelRequest>? controlPanelRequestSetup = null)
    {
        var ctx = new BunitContext();
        ctx.SetupSuiteServices(setup =>
        {
            setup.ClientMediator.Request<GetInstances, GetInstancesResponse>(
                Arg.Any<GetInstances>(), Arg.Any<CancellationToken>())
                .Returns(new GetInstancesResponse(instanceInformations ?? []));

            configure?.Invoke(setup);
        });

        ctx.SetupControlPanelServices(setup =>
        {
            controlPanelRequestSetup?.Invoke(setup);
        });

        ctx.JSInterop.ConfigureQuickGridJSInterop();
        ctx.Services.AddSingleton(_informationProvider);
        ctx.Services.AddControlPanelInfrastructure();
        ctx.Services.AddInstancesControlPanel();

        _informationProvider.GetInstancesInCluster(Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(instanceInformations?.Select(k => (IInstanceInformation)k).ToList() ?? []));

        return ctx;
    }

    public sealed class OnInitializedAsync : InstancesControlPanelTests
    {
        [Fact]
        public void Should_render_component()
        {
            // Arrange
            using var ctx = SetupTestContext();
            var state = new InstancesControlPanelState();

            // Act + Assert
            ctx.Render<InstancesControlPanel>(b => b.Add(p => p.State, state)).Should().NotBeNull();
        }

        [Fact]
        public async Task Should_load_instances()
        {
            // Arrange
            IUiMediator? mediator = null;
            await using var ctx = SetupTestContext(null, setup =>
            {
                mediator = setup.ClientMediator;
            });
            var state = new InstancesControlPanelState();

            var resetHandler = ctx.Services.GetRequiredService<IControlPanelResetHandler<InstancesControlPanelState>>();
            await resetHandler.Reset(state, TestContext.Current.CancellationToken);

            // Act
            var component = ctx.Render<InstancesControlPanel>(b => b.Add(p => p.State, state));

            // Assert
            component.Should().NotBeNull();
            mediator.Should().NotBeNull();

            await mediator.Received()
                .Request<GetInstances, GetInstancesResponse>(Arg.Is<GetInstances>(k => k.InstanceId == null), Arg.Any<CancellationToken>());
        }
    }

    public sealed class DeleteSelectedInstances : InstancesControlPanelTests
    {
        [Fact(Skip = "Incompatibility with bunit and virtual scrolling.")]
        public async Task Should_add_deleting_instance_to_list()
        {
            // Arrange
            IUiMediator? mediator = null;
            var info = CreateSlaveInstanceInfo();

            await using var ctx = SetupTestContext([info], setup =>
            {
                mediator = setup.ClientMediator;
            });
            var state = new InstancesControlPanelState();

            var resetHandler = ctx.Services.GetRequiredService<IControlPanelResetHandler<InstancesControlPanelState>>();
            await resetHandler.Reset(state, TestContext.Current.CancellationToken);

            // Act
            var component = ctx.Render<InstancesControlPanel>(b => b.Add(p => p.State, state));
            component.TriggerGridFirstRowSelectionChange(true);

            await component.FindGridActionButton(MonochromeIconName.Delete).ClickAsync();

            // Assert
            mediator.Should().NotBeNull();
            state.DeletingInstances.Count.Should().Be(1);
            state.DeletingInstances.First().Id.Should().Be(info.Id);
        }
    }

    public sealed class SynchronizeSelectedInstances : InstancesControlPanelTests
    {
        [Fact(Skip = "Incompatibility with bunit and virtual scrolling.")]
        public async Task Should_send_command_on_mediator()
        {
            // Arrange
            IUiMediator? mediator = null;
            var info = CreateSlaveInstanceInfo();

            await using var ctx = SetupTestContext([info], setup =>
            {
                mediator = setup.ClientMediator;
            });
            var state = new InstancesControlPanelState();

            var resetHandler = ctx.Services.GetRequiredService<IControlPanelResetHandler<InstancesControlPanelState>>();
            await resetHandler.Reset(state, TestContext.Current.CancellationToken);

            // Act
            var component = ctx.Render<InstancesControlPanel>(b => b.Add(p => p.State, state));
            component.TriggerGridFirstRowSelectionChange(true);

            await component.FindGridActionButton(MonochromeIconName.Reload).ClickAsync();

            // Assert
            mediator.Should().NotBeNull();
            //await mediator.Received(1).Send(
            //    Arg.Is((ControlInstance c) => c.InstanceId == info.Id &&
            //    c.Action == InstanceCommand.Synchronize));
        }
    }

    public sealed class EditInstance : InstancesControlPanelTests
    {
        [Fact(Skip = "Incompatibility with bunit and virtual scrolling.")]
        public async Task Should_send_control_panel_request()
        {
            // Arrange
            IControlPanelRequest? controlPanelRequest = null;
            var info = CreateSlaveInstanceInfo();

            await using var ctx = SetupTestContext([info], null, controlPanelRequestSetup =>
            {
                controlPanelRequest = controlPanelRequestSetup;
            });
            var state = new InstancesControlPanelState();

            var resetHandler = ctx.Services.GetRequiredService<IControlPanelResetHandler<InstancesControlPanelState>>();
            await resetHandler.Reset(state, TestContext.Current.CancellationToken);

            // Act
            var component = ctx.Render<InstancesControlPanel>(b => b.Add(p => p.State, state));
            await component.Find(".navigate-button").ClickAsync();

            // Assert
            controlPanelRequest.Should().NotBeNull();
            await controlPanelRequest.Received(1).Send<InstanceControlPanel, InstanceControlPanelState>(
                Arg.Any<Action<InstanceControlPanelState>>());
        }
    }

    public sealed class Consume : InstancesControlPanelTests
    {
        [Fact(Skip = "Incompatibility with bunit and virtual scrolling.")]
        public async Task Should_remove_deleted_instance()
        {
            // Arrange
            var info = CreateSlaveInstanceInfo();
            var instanceEvent = new ControlInstanceCompleted(info.Id, InstanceCommand.Delete);
            await using var ctx = SetupTestContext([info]);
            var state = new InstancesControlPanelState();

            var resetHandler = ctx.Services.GetRequiredService<IControlPanelResetHandler<InstancesControlPanelState>>();
            await resetHandler.Reset(state, TestContext.Current.CancellationToken);

            var component = ctx.Render<InstancesControlPanel>(b => b.Add(p => p.State, state));
            var clientContext = ClientContextFactory.Create(instanceEvent);
            component.Markup.Should().Contain(info.Name);

            // Act
            component.TriggerGridFirstRowSelectionChange(true);
            await component.FindGridActionButton(MonochromeIconName.Delete).ClickAsync();
            await component.Instance.Consume(clientContext, TestContext.Current.CancellationToken);

            // Assert
            component.Markup.Should().NotContain(info.Name);
        }

        [Fact(Skip = "Incompatibility with bunit and virtual scrolling.")]
        public async Task Should_reset_instance_is_synchronizing_flag()
        {
            // Arrange
            var info = CreateSlaveInstanceInfo();
            var instanceEvent = new ControlInstanceCompleted(info.Id, InstanceCommand.Synchronize);
            await using var ctx = SetupTestContext([info]);
            var state = new InstancesControlPanelState();

            var resetHandler = ctx.Services.GetRequiredService<IControlPanelResetHandler<InstancesControlPanelState>>();
            await resetHandler.Reset(state, TestContext.Current.CancellationToken);

            var component = ctx.Render<InstancesControlPanel>(b => b.Add(p => p.State, state));
            var clientContext = ClientContextFactory.Create(instanceEvent);

            component.Markup.Should().Contain(info.Name);

            // Act            
            await component.Instance.Consume(clientContext, TestContext.Current.CancellationToken);

            // Assert            
            // todo - we can't assert because we can't access the models yet.
        }
    }

    // todo - activate on next sdk v0.23.0
    //public class OnHealthStatusChanged : InstancesControlPanelTests
    //{
    //    [Fact]
    //    public void Changes_health_status_on_health_status_changed()
    //    {
    //        // Arrange
    //        var info = CreateSlaveInstanceInfo();
    //        using var ctx = SetupTestContext([info]);

    //        // Act
    //        var component = ctx.Render<InstancesControlPanel>();

    //        _informationProvider.HealthStatusChanged += Raise.Event<Func<Guid, HealthStatus, DateTimeOffset, Task>>(info.Id, HealthStatus.Healthy, DateTimeOffset.Now);

    //        // Assert
    //        component.Markup.Should().Contain(HealthStatus.Healthy.ToString());
    //    }
    //}
}
