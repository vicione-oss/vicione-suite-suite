using Blazor.Shared.Settings.Extensions;
using Blazor.Shared.Connections.Contracts;
using Blazor.Shared.Connections.Extensions;
using Blazor.Shared.Connections.Services;
using Blazor.Shared.Tests.Connections.Extensions;
using Blazor.Tests.Tools;
using Bunit;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Sdk.Client.ControlPanels.Services;
using Sdk.Client.Infrastructure;
using Sdk.Connections.Commands;
using Sdk.Connections.Contracts;
using Sdk.Connections.Events;
using Sdk.Connections.Requests;
using Sdk.Messaging;
using Sdk.Testing.Client;
using Xunit;
using SuiteConnectionServiceLocalization = Blazor.Shared.Connections.Services.Localization.SuiteConnectionService;

namespace Blazor.Shared.Tests.Connections.Services;

public class SuiteConnectionServiceTests
{
    public class UpsertTagOperations
    {
        [Fact]
        public async Task Should_create_tag()
        {
            // Arrange
            var tag = new Tag("test", Guid.NewGuid());

            var uiMediator = Substitute.For<IUiMediator>();

            using var suiteConnectionService = new SuiteConnectionService(uiMediator);

            uiMediator.SetupGetConnections();
            await suiteConnectionService.Initialize();

            uiMediator.Setup<UpsertTag, TagsChanged>(
                (command) => new TagsChanged(command.CorrelationId, CrudAction.Created, [command.Tag]),
                () => suiteConnectionService);

            // Act
            await suiteConnectionService.UpsertTag(tag);

            // Assert
            suiteConnectionService.Tags.Should().BeEquivalentTo([tag]);

            await uiMediator.Received(1).Send(Arg.Any<UpsertTag>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_update_existing_tag()
        {
            // Arrange
            var tag = new Tag("test", Guid.NewGuid());

            var uiMediator = Substitute.For<IUiMediator>();

            using var suiteConnectionService = new SuiteConnectionService(uiMediator);

            uiMediator.SetupGetConnections();
            await suiteConnectionService.Initialize();

            var eventFactoryCallCount = 0;

            uiMediator.Setup<UpsertTag, TagsChanged>(
                (command) => new TagsChanged(command.CorrelationId, eventFactoryCallCount++ == 0 ? CrudAction.Created : CrudAction.Updated, [command.Tag]),
                () => suiteConnectionService);

            await suiteConnectionService.UpsertTag(tag);

            tag.Text = "newText";

            // Act
            await suiteConnectionService.UpsertTag(tag);

            // Assert
            suiteConnectionService.Tags.Should().BeEquivalentTo([tag]);
            suiteConnectionService.Tags.First().Text.Should().Be(tag.Text);

            await uiMediator.Received(2).Send(Arg.Any<UpsertTag>(), Arg.Any<CancellationToken>());
        }
    }

    public class DeleteTagOperations
    {
        [Fact]
        public async Task Should_remove_specific_tag_from_list()
        {
            // Arrange
            var deleteTag = new Tag("delete", Guid.NewGuid());
            var keepTag = new Tag("keep", Guid.NewGuid());

            var tagMap = (new Tag[] { deleteTag, keepTag }).ToDictionary(tag => tag.Id);

            var uiMediator = Substitute.For<IUiMediator>();

            using var suiteConnectionService = new SuiteConnectionService(uiMediator);

            uiMediator.SetupGetConnections();
            await suiteConnectionService.Initialize();

            uiMediator.Setup<UpsertTag, TagsChanged>(
                (command) => new TagsChanged(command.CorrelationId, CrudAction.Created, [command.Tag]),
                () => suiteConnectionService);

            uiMediator.Setup<DeleteTag, TagsChanged>(
                (command) => new TagsChanged(command.CorrelationId, CrudAction.Deleted, [tagMap[command.TagId]]),
                () => suiteConnectionService);

            await suiteConnectionService.UpsertTag(deleteTag);
            await suiteConnectionService.UpsertTag(keepTag);

            // Act
            await suiteConnectionService.DeleteTag(deleteTag);

            // Assert
            suiteConnectionService.Tags.Should().BeEquivalentTo([keepTag]);

            await uiMediator.Received(1).Send(Arg.Any<DeleteTag>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_deny_delete_of_protected_tag()
        {
            // Arrange
            var protectedTag = new Tag("delete", Guid.NewGuid())
            {
                Protected = true
            };

            var tagMap = (new Tag[] { protectedTag }).ToDictionary(tag => tag.Id);

            var uiMediator = Substitute.For<IUiMediator>();

            using var suiteConnectionService = new SuiteConnectionService(uiMediator);

            uiMediator.SetupGetConnections();
            await suiteConnectionService.Initialize();

            uiMediator.Setup<UpsertTag, TagsChanged>(
                (command) => new TagsChanged(command.CorrelationId, CrudAction.Created, [command.Tag]),
                () => suiteConnectionService);

            uiMediator.Setup<DeleteTag, TagsChanged>(
                (command) => new TagsChanged(command.CorrelationId, CrudAction.Deleted, [tagMap[command.TagId]]),
                () => suiteConnectionService);

            await suiteConnectionService.UpsertTag(protectedTag);

            // Act
            var result = await suiteConnectionService.DeleteTag(protectedTag);

            // Assert
            result.Should().BeOfType<SuiteConnectionServiceErrorResult>().Subject.ErrorMessage.Should().Be(SuiteConnectionServiceLocalization.TagCannotBeDeletedBecauseItIsProtected);
            suiteConnectionService.Tags.Should().BeEquivalentTo([protectedTag]);

            await uiMediator.Received(0).Send(Arg.Any<DeleteTag>(), Arg.Any<CancellationToken>());
        }
    }

    public class ConsumeConnectionChangedEvent
    {
        [Fact]
        public async Task Created_event_adds_connection_and_updates_state()
        {
            // Arrange
            var uiMediator = Substitute.For<IUiMediator>();
            uiMediator.SetupGetConnections();

            using var suiteConnectionService = new SuiteConnectionService(uiMediator);
            await suiteConnectionService.Initialize();

            var originalConnection = ConnectionFactory.CreateConnections().First();

            // Assert Event
            var stateChangedCallsCount = 0;
            suiteConnectionService.ConnectionStateChanged += (connections) =>
            {
                Assert.NotNull(connections);
                var connection = Assert.Single(connections);
                originalConnection.AssertEqual(connection);
                stateChangedCallsCount++;
                return Task.CompletedTask;
            };

            uiMediator.SetupGetSingleConnection(originalConnection);

            var correlationId = Guid.NewGuid();

            // Act
            await suiteConnectionService.ConsumeConnectionChanged(correlationId, CrudAction.Created, originalConnection);

            // Assert
            stateChangedCallsCount.Should().Be(1);
        }

        [Fact]
        public async Task Updated_event_updates_connection_and_state()
        {
            // Arrange
            var mock = new SuiteConnectionServiceTests();
            var connection = ConnectionFactory.SQLiteConnection;
            var updatedConnection = ConnectionFactory.UpdatedConnection(connection.Id);

            var uiMediator = Substitute.For<IUiMediator>();
            uiMediator.SetupGetSingleConnection(connection);

            using var suiteConnectionService = new SuiteConnectionService(uiMediator);
            await suiteConnectionService.Initialize();

            uiMediator.SetupGetSingleConnection(updatedConnection);

            // Assert Event
            var stateChangedCallsCount = 0;
            suiteConnectionService.ConnectionStateChanged += (connections) =>
            {
                Assert.NotNull(connections);
                var conn = Assert.Single(connections);
                updatedConnection.AssertEqual(conn);
                stateChangedCallsCount++;
                return Task.CompletedTask;
            };

            var correlationId = Guid.NewGuid();

            // Act
            await suiteConnectionService.ConsumeConnectionChanged(correlationId, CrudAction.Updated, updatedConnection);

            // Assert
            stateChangedCallsCount.Should().Be(1);
        }

        [Fact]
        public async Task Deleted_event_removes_connection_and_updates_state()
        {
            // Arrange
            var mock = new SuiteConnectionServiceTests();
            var connection = ConnectionFactory.SQLiteConnection;

            var uiMediator = Substitute.For<IUiMediator>();
            uiMediator.SetupGetSingleConnection(connection);

            using var suiteConnectionService = new SuiteConnectionService(uiMediator);
            await suiteConnectionService.Initialize();

            // Assert Event
            var stateChangedCallsCount = 0;
            suiteConnectionService.ConnectionStateChanged += (connections) =>
            {
                Assert.NotNull(connections);
                Assert.Empty(connections);
                stateChangedCallsCount++;
                return Task.CompletedTask;
            };

            var correlationId = Guid.NewGuid();

            // Act
            await suiteConnectionService.ConsumeConnectionChanged(correlationId, CrudAction.Deleted, connection);

            // Assert
            stateChangedCallsCount.Should().Be(1);
        }
    }

    public class UpsertConnectionOperations
    {
        [Fact]
        public async Task Should_create_connection()
        {
            // Arrange
            var tag = new Tag("test", Guid.NewGuid());
            var connection = new Connection();
            connection.Tags.Add(tag);

            var uiMediator = Substitute.For<IUiMediator>();

            using var suiteConnectionService = new SuiteConnectionService(uiMediator);

            uiMediator.SetupGetConnections();
            await suiteConnectionService.Initialize();

            uiMediator.Setup<UpsertConnection, ConnectionChanged>(
                (command) => new ConnectionChanged(command.CorrelationId, CrudAction.Created, command.Connection, [.. command.Connection.Tags], []),
                () => suiteConnectionService);

            // Act
            await suiteConnectionService.UpsertConnection(connection);

            // Assert
            suiteConnectionService.Tags.Should().BeEquivalentTo([tag]);
            suiteConnectionService.Connections.Should().BeEquivalentTo([connection]);

            await uiMediator.Received(1).Send(Arg.Any<UpsertConnection>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Should_update_existing_connection()
        {
            // Arrange
            var uiMediator = Substitute.For<IUiMediator>();

            using var suiteConnectionService = new SuiteConnectionService(uiMediator);

            uiMediator.SetupGetConnections();
            await suiteConnectionService.Initialize();

            uiMediator.Setup<UpsertConnection, ConnectionChanged>(
                (command) => new ConnectionChanged(command.CorrelationId, CrudAction.Updated, command.Connection, [], []),
                () => suiteConnectionService);

            var databaseConnection = ConnectionFactory.SQLiteConnection;

            // Act
            var result = await suiteConnectionService.UpsertConnection(databaseConnection);

            // Assert
            await uiMediator.Received().Send(Arg.Is<UpsertConnection>(a => a.Connection == databaseConnection), Arg.Any<CancellationToken>());

            Assert.IsType<SuiteConnectionServiceSuccessResult>(result);
        }

        [Fact]
        public async Task Update_connection_timed_out()
        {
            // Arrange
            var uiMediator = Substitute.For<IUiMediator>();

            using var suiteConnectionService = new SuiteConnectionService(uiMediator);

            uiMediator.SetupGetConnections();
            await suiteConnectionService.Initialize();

            var unknownCorrelationId = Guid.NewGuid(); // unknown correlation-id causes timeout

            uiMediator.Setup<UpsertConnection, ConnectionChanged>(
                (command) => new ConnectionChanged(unknownCorrelationId, CrudAction.Updated, command.Connection, [], []),
                () => suiteConnectionService);

            // Act
            var result = await suiteConnectionService.UpsertConnection(ConnectionFactory.SQLiteConnection);

            // Assert
            result.Should().BeOfType<SuiteConnectionServiceErrorResult>().Subject.ErrorMessage.Should().Contain("timed out");
        }
    }

    public class DeleteConnectionOperations
    {
        [Fact]
        public async Task Should_delete_connection()
        {
            // Arrange
            var tag = new Tag("test", Guid.NewGuid());
            var connection = new Connection();
            connection.Tags.Add(tag);

            var connectionMap = (new Connection[] { connection }).ToDictionary(connection => connection.Id);

            var uiMediator = Substitute.For<IUiMediator>();

            using var suiteConnectionService = new SuiteConnectionService(uiMediator);

            uiMediator.SetupGetConnections();
            await suiteConnectionService.Initialize();

            uiMediator.Setup<UpsertConnection, ConnectionChanged>(
                    (command) => new ConnectionChanged(command.CorrelationId, CrudAction.Created, command.Connection, [.. command.Connection.Tags], []),
                    () => suiteConnectionService);

            uiMediator.Setup<DeleteConnection, ConnectionChanged>(
                (command) => new ConnectionChanged(command.CorrelationId, CrudAction.Deleted, connectionMap[command.ConnectionId], [], []),
                () => suiteConnectionService);

            await suiteConnectionService.UpsertConnection(connection);

            // Act
            await suiteConnectionService.DeleteConnection(connection);

            // Assert
            suiteConnectionService.Tags.Should().BeEquivalentTo([tag]);
            suiteConnectionService.Connections.Should().BeEmpty();

            await uiMediator.Received(1).Send(Arg.Any<DeleteConnection>(), Arg.Any<CancellationToken>());
        }
    }

    public class ConnectionServiceUpsertConnection
    {
        private static TestContext SetupTestContext(Connection? connection = null, Action<ClientServiceConfigurator>? additionalSetup = null)
        {
            var ctx = new TestContext();

            ctx.SetupSuiteServicesWithBlazorDx(setup =>
            {
                setup.ClientMediator.Request<GetConnections, GetConnectionsResponse>(Arg.Any<GetConnections>(), Arg.Any<CancellationToken>())
                    .Returns(new GetConnectionsResponse(connection is not null ? [connection] : []));

                setup.ClientMediator.Request<GetTags, GetTagsResponse>(Arg.Any<GetTags>(), Arg.Any<CancellationToken>())
                    .Returns(new GetTagsResponse([]));

                if (additionalSetup is not null)
                    additionalSetup(setup);
            });

            ctx.Services.AddScoped(_ => Substitute.For<IActiveControlPanelDescriptorProvider>())
                .AddControlPanelInfrastructure()
                .AddConnectionControlPanel();

            return ctx;
        }

        [Fact]
        public async Task Update_connection_successful()
        {
            // Arrange
            var databaseConnection = ConnectionFactory.SQLiteConnection;
            var correlationId = Guid.Empty;
            ISuiteConnectionService connectionService = default!;

            void setupSuiteService(ClientServiceConfigurator setup)
                => setup.ClientMediator.When(m => m.Send(Arg.Any<UpsertConnection>(), Arg.Any<CancellationToken>()))
                    .Do(async callinfo =>
                    {
                        var command = callinfo.Arg<UpsertConnection>();
                        correlationId = command.CorrelationId;
                        var connection = command.Connection;

                        var message = new ConnectionChanged(correlationId, CrudAction.Updated, connection, [], []);
                        var context = new ClientContext<ConnectionChanged>(message, Guid.NewGuid());

                        await connectionService.Initialize();
                        if (connectionService is IEventConsumer<ConnectionChanged> eventConsumer)
                            await eventConsumer.Consume(context, CancellationToken.None);
                    });

            using var ctx = SetupTestContext(databaseConnection, setupSuiteService);

            connectionService = ctx.Services.GetRequiredService<ISuiteConnectionService>();

            var uiMediator = ctx.Services.GetRequiredService<IUiMediator>();

            // Act
            var serviceResult = await connectionService.UpsertConnection(databaseConnection, CancellationToken.None);

            // Assert
            await uiMediator.Received().Send(Arg.Is<UpsertConnection>(a => a.Connection == databaseConnection), Arg.Any<CancellationToken>());

            Assert.IsType<SuiteConnectionServiceSuccessResult>(serviceResult);
        }

        [Fact]
        public async Task Update_connection_failed_timeout_occured()
        {
            // Arrange
            var databaseConnection = ConnectionFactory.SQLiteConnection;
            var correlationId = Guid.Empty;
            ISuiteConnectionService connectionService = default!;

            void setupSuiteService(ClientServiceConfigurator setup)
                => setup.ClientMediator.When(m => m.Send(Arg.Any<UpsertConnection>(), Arg.Any<CancellationToken>()))
                    .Do(async callinfo =>
                    {
                        var command = callinfo.Arg<UpsertConnection>();
                        correlationId = Guid.NewGuid(); // unknown correlation-id causes timeout
                        var connection = command.Connection;

                        var message = new ConnectionChanged(correlationId, CrudAction.Updated, connection, [], []);
                        var context = new ClientContext<ConnectionChanged>(message, Guid.NewGuid());

                        await connectionService.Initialize();
                        if (connectionService is IEventConsumer<ConnectionChanged> eventConsumer)
                            await eventConsumer.Consume(context, CancellationToken.None);
                    });

            using var ctx = SetupTestContext(databaseConnection, setupSuiteService);

            connectionService = ctx.Services.GetRequiredService<ISuiteConnectionService>();

            var uiMediator = ctx.Services.GetRequiredService<IUiMediator>();

            // Act
            var serviceResult = await connectionService.UpsertConnection(databaseConnection, CancellationToken.None);

            // Assert
            await uiMediator.Received().Send(Arg.Is<UpsertConnection>(a => a.Connection == databaseConnection), Arg.Any<CancellationToken>());

            Assert.IsType<SuiteConnectionServiceErrorResult>(serviceResult);
        }
    }
}
