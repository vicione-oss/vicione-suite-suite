using Core.OS.Connections.Consumers;
using Core.OS.DbContext;
using Core.Shared.Connections.Commands;
using Core.Shared.Connections.Events;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Sdk.Connections.Contracts;
using Sdk.Testing.Backend;

namespace Core.OS.Tests.Connections.Consumers;

public sealed class TestConnectionConsumerTests
{
    private readonly IConnectionTypeRegistry _connectionTypeRegistry = Substitute.For<IConnectionTypeRegistry>();
    private readonly Action<IBusRegistrationConfigurator> _configureServices;

    public TestConnectionConsumerTests()
        => _configureServices = cfg =>
        {
            cfg.AddConsumer<TestConnectionConsumer>();
            cfg.AddSingleton<IRoutingSlipBuilder>(new RoutingSlipBuilder(NewId.NextGuid()));
            cfg.AddSingleton(_ => _connectionTypeRegistry);
            cfg.AddSingleton(_ => new TestConfig().BuildConfiguration());
        };

    private static TestConnection SetupCommandWithMqtt(MqttConnectionType protocol)
        => new(Guid.NewGuid(), ConnectionFactory.CreateMqttConnection("localhost", 1883, protocol));

    private static TestConnection SetupCommandWithSqliteDatabase(string connectionString = "DataSource=:memory:")
        => new(Guid.NewGuid(),
            ConnectionFactory.CreateSQLiteConnection(connectionString));

    private void SetupTestServiceForError(Guid connectionId)
    {
        var connectionTest = Substitute.For<IConnectionTest>();
        connectionTest.Test(Arg.Any<IConnection>(), Arg.Any<CancellationToken>())
            .Returns(new ConnectionTestResult(false, new(110, "Error")));
        _connectionTypeRegistry.TryGetConnectionTest(Arg.Any<string>(), out Arg.Any<IConnectionTest?>())
            .Returns(c =>
            {
                c[1] = connectionTest;
                return true;
            });
        var connectionSerializer = Substitute.For<IConnectionSerializer>();
        _connectionTypeRegistry.TryGetConnectionSerializer(Arg.Any<string>(), out Arg.Any<IConnectionSerializer?>())
            .Returns(c =>
            {
                c[1] = connectionSerializer;
                return true;
            });
    }

    private void SetupTestServiceForSuccess(Guid connectionId)
    {
        var connectionTest = Substitute.For<IConnectionTest>();
        connectionTest.Test(Arg.Any<IConnection>(), Arg.Any<CancellationToken>())
            .Returns(new ConnectionTestResult(true, null));
        _connectionTypeRegistry.TryGetConnectionTest(Arg.Any<string>(), out Arg.Any<IConnectionTest?>())
            .Returns(c =>
            {
                c[1] = connectionTest;
                return true;
            });
        var connectionSerializer = Substitute.For<IConnectionSerializer>();
        _connectionTypeRegistry.TryGetConnectionSerializer(Arg.Any<string>(), out Arg.Any<IConnectionSerializer?>())
            .Returns(c =>
            {
                c[1] = connectionSerializer;
                return true;
            });
    }

    [Fact]
    public async Task Should_publish_error_for_empty_mqtt_connection()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var command = new TestConnection(Guid.NewGuid(), new Connection { Type = ConnectionType.Mqtt });

        SetupTestServiceForError(command.Connection.Id);

        // Act
        var doneEvent = await tester.TestCommand<TestConnection, TestConnectionConsumer, TestConnectionDoneEvent>(command);

        // Assert
        doneEvent.CorrelationId.Should().Be(command.CorrelationId);
        doneEvent.TestResult.Success.Should().BeFalse();
        doneEvent.TestResult.ErrorInfo.Should().NotBeNull();
    }

    [Fact]
    public async Task Should_publish_success_event_for_mqtt_tcp_connection()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var command = SetupCommandWithMqtt(MqttConnectionType.TCP);
        SetupTestServiceForSuccess(command.Connection.Id);

        // Act
        var doneEvent = await tester.TestCommand<TestConnection, TestConnectionConsumer, TestConnectionDoneEvent>(command);

        // Assert
        doneEvent.CorrelationId.Should().Be(command.CorrelationId);
        doneEvent.TestResult.Success.Should().BeTrue();
        doneEvent.TestResult.ErrorInfo.Should().BeNull();
        // VerifyMqttConnect(command, mqttConnection, false);
    }

    [Fact]
    public async Task Should_publish_success_event_for_mqtt_tcp_with_tls_connection()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var command = SetupCommandWithMqtt(MqttConnectionType.TCP);

        SetupTestServiceForSuccess(command.Connection.Id);

        // Act
        var doneEvent = await tester.TestCommand<TestConnection, TestConnectionConsumer, TestConnectionDoneEvent>(command);

        // Assert
        doneEvent.CorrelationId.Should().Be(command.CorrelationId);
        doneEvent.TestResult.Success.Should().BeTrue();
        doneEvent.TestResult.ErrorInfo.Should().BeNull();

        // VerifyMqttConnect(command, mqttConnection, true);
    }

    [Fact]
    public async Task Should_publish_success_event_for_mqtt_websocket_connection()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var command = SetupCommandWithMqtt(MqttConnectionType.WebSocket);

        SetupTestServiceForSuccess(command.Connection.Id);

        // Act
        var doneEvent = await tester.TestCommand<TestConnection, TestConnectionConsumer, TestConnectionDoneEvent>(command);

        // Assert
        doneEvent.CorrelationId.Should().Be(command.CorrelationId);
        doneEvent.TestResult.Success.Should().BeTrue();
        doneEvent.TestResult.ErrorInfo.Should().BeNull();

        // _mqttClientMock.Verify(mc => mc.ConnectAsync(
        //     It.Is<MqttClientOptions>(mco =>
        //         (mco.ChannelOptions as MqttClientWebSocketOptions) is not null &&
        //         ((MqttClientWebSocketOptions)mco.ChannelOptions).Uri
        //         == $"ws://{mqttConnection.Address}:{mqttConnection.Port}/" &&
        //         mco.Credentials.GetUserName(mco) == command.Connection.Username &&
        //         mco.Credentials.GetPassword(mco)
        //             .SequenceEqual(Encoding.UTF8.GetBytes(command.Connection.Password ?? string.Empty)) &&
        //         mco.ChannelOptions.TlsOptions.UseTls == false
        //     ),
        //     It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task Should_publish_error_for_empty_database_connection()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var command = new TestConnection(Guid.NewGuid(), new Connection { Type = ConnectionType.SQLite });
        SetupTestServiceForError(command.Connection.Id);

        // Act
        var doneEvent = await tester.TestCommand<TestConnection, TestConnectionConsumer, TestConnectionDoneEvent>(command);

        // Assert
        doneEvent.CorrelationId.Should().Be(command.CorrelationId);
        doneEvent.TestResult.Success.Should().BeFalse();
        doneEvent.TestResult.ErrorInfo.Should().NotBeNull();
    }

    [Fact]
    public async Task Should_publish_success_event_for_sqlite_database_connection()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        await using var context = TestDbContextFactory.CreateSqliteContext<ConnectionDbContextSqlite>();
        var command = SetupCommandWithSqliteDatabase();
        SetupTestServiceForSuccess(command.Connection.Id);

        // Act
        var doneEvent = await tester.TestCommand<TestConnection, TestConnectionConsumer, TestConnectionDoneEvent>(command);

        // Assert
        doneEvent.CorrelationId.Should().Be(command.CorrelationId);
        doneEvent.TestResult.Success.Should().BeTrue();
        doneEvent.TestResult.ErrorInfo.Should().BeNull();
    }

    [Fact]
    public async Task Should_publish_error_for_sqlite_database_connection_on_failure()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var command = SetupCommandWithSqliteDatabase("DataSource=/Appdata/test.db");
        SetupTestServiceForError(command.Connection.Id);

        // Act
        var doneEvent = await tester.TestCommand<TestConnection, TestConnectionConsumer, TestConnectionDoneEvent>(command);

        // Assert
        doneEvent.CorrelationId.Should().Be(command.CorrelationId);
        doneEvent.TestResult.Success.Should().BeFalse();
        doneEvent.TestResult.ErrorInfo.Should().NotBeNull();
    }
}
