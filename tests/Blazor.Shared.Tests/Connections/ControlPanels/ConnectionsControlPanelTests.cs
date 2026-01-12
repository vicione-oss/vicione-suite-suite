using AwesomeAssertions;
using Blazor.Shared.Connections.ControlPanels;
using Blazor.Shared.Connections.Extensions;
using Blazor.Shared.Connections.Services;
using Blazor.Shared.Tests.Connections.Extensions;
using Blazor.Tests.Tools;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Sdk.Client.Connections;
using Sdk.Client.ControlPanels.Services;
using Sdk.Client.Infrastructure;
using Sdk.Connections.Contracts;
using Sdk.Testing.Client;
using ViciOne.Ui.MonochromeIcons.Core.Enums;
using Xunit;

namespace Blazor.Shared.Tests.Connections.ControlPanels;

public class ConnectionsControlPanelTests
{
    internal readonly ISuiteConnectionService _connectionService = Substitute.For<ISuiteConnectionService>();
    private IControlPanelRequest? _controlPanelRequest;

    private TestContext SetupTestContext()
    {
        var ctx = new TestContext();
        ctx.SetupSuiteServices()
            .SetupSuiteServicesWithBlazorDx()
            .SetupControlPanelServices(setup =>
            {
                _controlPanelRequest = setup;
            });

        _connectionService.GetTags(Arg.Any<CancellationToken>()).Returns([]);

        ctx.Services
            .AddSingleton(_connectionService)
            .AddSingleton(_ => SetupConnectionTypeRegistry())
            .AddSingleton(_ => Substitute.For<IConnectionTypeUiRegistry>())
            .AddConnectionsControlPanel();

        ctx.JSInterop.ConfigureQuickGridJSInterop();
        ctx.JSInterop.SetupModule();

        return ctx;
    }

    private static IConnectionTypeRegistry SetupConnectionTypeRegistry()
    {
        var registry = Substitute.For<IConnectionTypeRegistry>();

        registry.GetConnectionTypes().Returns([ConnectionType.Mqtt]);
        registry.TryCreateConnection(Arg.Any<string>(), out Arg.Any<IConnection?>())
            .Returns(c =>
            {
                c[1] = Substitute.For<IConnection>();
                return true;
            });
        registry.TryGetConnectionSerializer(Arg.Any<string>(), out Arg.Any<IConnectionSerializer?>())
            .Returns(c =>
            {
                c[1] = Substitute.For<IConnectionSerializer>();
                return true;
            });

        return registry;
    }

    public class AddNewItem : ConnectionsControlPanelTests
    {
        [Fact(Skip = "Incompatibility with bunit and virtual scrolling.")]
        public void Should_send_control_panel_request_with_connection_id_null()
        {
            // Arrange
            using var ctx = SetupTestContext();
            var mediator = Substitute.For<IUiMediator>();
            using var state = new ConnectionsControlPanelState(mediator);
            var component = ctx.RenderControlPanelPage<ConnectionsControlPanel, ConnectionsControlPanelState>(state);

            // Act
            component.FindGridActionButton(MonochromeIconName.PlusSlim).Click();

            // Assert
            Assert.NotNull(_controlPanelRequest);
            _controlPanelRequest.Received()
                .Send<ConnectionControlPanel, ConnectionControlPanelState>(
                    Arg.Any<Action<ConnectionControlPanelState>>());

            // todo - check state
        }
    }

    public class EditSelectedItem : ConnectionsControlPanelTests
    {
        [Fact(Skip = "Incompatibility with bunit and virtual scrolling.")]
        public void Should_send_control_panel_request_with_connection_id()
        {
            // Arrange
            using var ctx = SetupTestContext();
            var connections = new List<Connection>
            {
                ConnectionFactory.SQLiteConnection
            };

            var mediator = Substitute.For<IUiMediator>();
            using var state = new ConnectionsControlPanelState(mediator);
            var component = ctx.RenderControlPanelPage<ConnectionsControlPanel, ConnectionsControlPanelState>(state);

            _connectionService.ConnectionStateChanged += Raise.Event<Func<IReadOnlyList<Connection>, Task>?>(connections);

            // Act
            component.FindGridActionButton(MonochromeIconName.Edit).Click();

            // Assert
            Assert.NotNull(_controlPanelRequest);
            _controlPanelRequest.Received()
                .Send<ConnectionControlPanel, ConnectionControlPanelState>(
                    Arg.Any<Action<ConnectionControlPanelState>>());

            // todo - check state
        }
    }

    public class DeleteSelectedItems : ConnectionsControlPanelTests
    {
        [Fact(Skip = "Incompatibility with bunit and virtual scrolling.")]
        public void Adds_correct_connection_to_list()
        {
            // Arrange
            using var ctx = SetupTestContext();
            var connection = ConnectionFactory.SQLiteConnection;
            var connections = new List<Connection>
            {
                connection
            };

            var mediator = Substitute.For<IUiMediator>();
            using var state = new ConnectionsControlPanelState(mediator);
            var component = ctx.RenderControlPanelPage<ConnectionsControlPanel, ConnectionsControlPanelState>(state);

            _connectionService.ConnectionStateChanged += Raise.Event<Func<IReadOnlyList<Connection>, Task>?>(connections);

            component.TriggerGridFirstRowSelectionChange(true);

            // Act
            component.FindGridActionButton(MonochromeIconName.Delete).Click();

            // Assert
            state.DeletingConnections.Count.Should().Be(1);
            state.DeletingConnections.First().Connection.Should().BeEquivalentTo(connection);
        }
    }

    public class OnConnectionStateChanged : ConnectionsControlPanelTests
    {
        [Fact(Skip = "Incompatibility with bunit and virtual scrolling.")]
        public void Renders_when_new_connection_is_added()
        {
            // Arrange
            using var ctx = SetupTestContext();
            var connections = new List<Connection>
            {
                ConnectionFactory.SQLiteConnection,
                ConnectionFactory.HttpConnection,
            };

            var mediator = Substitute.For<IUiMediator>();
            using var state = new ConnectionsControlPanelState(mediator);
            var component = ctx.RenderControlPanelPage<ConnectionsControlPanel, ConnectionsControlPanelState>(state);

            // Act
            _connectionService.ConnectionStateChanged += Raise.Event<Func<IReadOnlyList<Connection>, Task>?>(connections);

            // Assert
            component.RenderCount.Should().Be(3, "3 -> ControlPageRendering + StateChange");
        }
    }
}
