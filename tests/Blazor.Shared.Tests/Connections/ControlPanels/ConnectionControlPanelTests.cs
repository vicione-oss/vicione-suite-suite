using Blazor.Shared.Connections.Contracts;
using Blazor.Shared.Connections.ControlPanels;
using Blazor.Shared.Connections.Extensions;
using Blazor.Shared.Connections.Factories;
using Blazor.Shared.Connections.Services;
using Blazor.Shared.Services;
using Blazor.Shared.Tests.Connections.Extensions;
using Blazor.Tests.Tools;
using Bunit;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Sdk.Client.Connections;
using Sdk.Client.ControlPanels.Services;
using Sdk.Connections.Contracts;
using Sdk.Connections.Requests;
using Sdk.Testing.Client;
using ViciOne.Ui.Localization.Resources;
using Xunit;

namespace Blazor.Shared.Tests.Connections.ControlPanels;

public class ConnectionControlPanelTests
{
    private static TestContext SetupTestContext(Connection? connection = null)
    {
        var ctx = new TestContext();

        var suiteConnectionService = Substitute.For<ISuiteConnectionService>();

        ctx.Services.AddScoped(_ => Substitute.For<IActiveControlPanelDescriptorProvider>())
            .AddScoped(_ => suiteConnectionService)
            .AddScoped(_ => Substitute.For<IControlPanelSaveHandler<ConnectionControlPanelState>>())
            .AddSingleton(_ => SetupConnectionTypeRegistry())
            .AddSingleton(_ => Substitute.For<IConnectionTypeUiRegistry>())
            .AddConnectionControlPanel();

        var correlationId = Guid.Empty;

        ctx.SetupSuiteServicesWithBlazorDx(setup =>
        {
            setup.ClientMediator.Request<GetConnections, GetConnectionsResponse>(Arg.Any<GetConnections>(), Arg.Any<CancellationToken>())
                .Returns(new GetConnectionsResponse(connection is not null ? [connection] : []));

            setup.ClientMediator.Request<GetTags, GetTagsResponse>(Arg.Any<GetTags>(), Arg.Any<CancellationToken>())
                .Returns(new GetTagsResponse([]));

            suiteConnectionService.GetConnection(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(connection);

            suiteConnectionService.GetTags(Arg.Any<CancellationToken>()).Returns([new Tag() { Id = Guid.NewGuid(), Text = "Tag A" }]);

            suiteConnectionService.UpsertConnection(Arg.Any<Connection>(), Arg.Any<CancellationToken>())
                .Returns(new SuiteConnectionServiceSuccessResult());
        });

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
        registry.TryGetConnectionTest(Arg.Any<string>(), out Arg.Any<IConnectionTest?>())
            .Returns(c =>
            {
                c[1] = Substitute.For<IConnectionTest>();
                return true;
            });

        return registry;
    }

    public class OnInitializedAsync : ConnectionControlPanelTests
    {
        [Fact]
        public void Should_init_empty_connection_if_state_connection_id_is_not_set()
        {
            // Arrange
            using var ctx = SetupTestContext();

            var registry = ctx.Services.GetRequiredService<IConnectionTypeRegistry>();
            var connection = EditConnectionModelFactory.CreateNew(registry);

            // Act
            var component = ctx.RenderComponent<ConnectionControlPanel>(parameters =>
            {
                parameters.Add(c => c.State, new());
            });

            // Assert
            component.AssertSettingsFieldTextBox(CommonVocabulary.Name, connection.Name);
            component.AssertSettingsFieldTextBox(CommonVocabulary.Description, connection.Description);
        }

        [Fact]
        public void Should_bind_connection_from_state_id()
        {
            // Arrange
            var connection = ConnectionFactory.DatabaseConnection;

            using var ctx = SetupTestContext(connection);

            // Act
            var component = ctx.RenderComponent<ConnectionControlPanel>(parameters =>
            {
                parameters.Add(c => c.State, new() { ConnectionId = connection.Id });
            });

            // Assert
            component.AssertSettingsFieldTextBox(CommonVocabulary.Name, connection.Name);
            component.AssertSettingsFieldTextBox(CommonVocabulary.Description, connection.Description);
        }

        [Fact]
        public async Task Should_throw_if_connection_from_state_can_not_be_found()
        {
            // Arrange
            var connection = ConnectionFactory.DatabaseConnection;

            using var ctx = SetupTestContext();

            // Act
            Func<Task> action = async () =>
            {
                ctx.RenderComponent<ConnectionControlPanel>(parameters =>
                {
                    parameters.Add(c => c.State, new() { ConnectionId = connection.Id });
                });

                await Task.CompletedTask;
            };

            // Assert
            await action.Should().ThrowAsync<InvalidOperationException>();
        }
    }

    public class OnSave : ConnectionControlPanelTests
    {
        [Fact]
        public async Task Should_call_save_handler()
        {
            // Arrange
            using var ctx = SetupTestContext();

            var controlPanelService = ReplaceControlPanelService(ctx.Services);

            var saveHandler = ctx.Services.GetRequiredService<IControlPanelSaveHandler<ConnectionControlPanelState>>();

            var controlPanelState = new ConnectionControlPanelState();
            var component = ctx.RenderComponent<ConnectionControlPanel>(parameters =>
            {
                parameters.Add(c => c.State, controlPanelState);
            });

            // Act
            await controlPanelService.BeginEdit();
            await controlPanelService.FinishEdit();

            // Assert
            await saveHandler.Received().Save(controlPanelState, Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_throw_exception_if_validation_fails()
        {
            // Arrange
            var connection = ConnectionFactory.DatabaseConnection;

            using var ctx = SetupTestContext(connection);

            var controlPanelService = ReplaceControlPanelService(ctx.Services);

            _ = ctx.RenderComponent<ConnectionControlPanel>(parameters =>
            {
                parameters.Add(c => c.State, new());
            });

            // Act
            await controlPanelService.BeginEdit();
            // TODO var action = () => controlPanelService.FinishEdit();

            // Assert
            // TODO 
            // action.Throws<InvalidOperationException>();
            // await Assert.ThrowsAsync<InvalidOperationException> (() => controlPanelService.FinishEdit());
        }
    }

    public class OnCancel : ConnectionControlPanelTests
    {
        [Fact]
        public void Should_reset_edited_values()
        {
            // Arrange
            var connection = ConnectionFactory.DatabaseConnection;

            using var ctx = SetupTestContext(connection);

            var controlPanelService = ctx.Services.GetRequiredService<IControlPanelService>();

            _ = ctx.RenderComponent<ConnectionControlPanel>(parameters =>
            {
                parameters.Add(c => c.State, new());
            });

            // Act
            controlPanelService.BeginEdit();
            controlPanelService.CancelEdit();

            // Assert
            controlPanelService.IsDirty.Should().BeFalse();
        }
    }

    private static IControlPanelService ReplaceControlPanelService(TestServiceProvider services)
    {
        var serviceDescriptor = services.First(descriptor => descriptor.ServiceType == typeof(IControlPanelService));
        Assert.True(services.Remove(serviceDescriptor));

        services.AddSingleton<IControlPanelService, ControlPanelService>();
        return services.GetRequiredService<IControlPanelService>();
    }
}
