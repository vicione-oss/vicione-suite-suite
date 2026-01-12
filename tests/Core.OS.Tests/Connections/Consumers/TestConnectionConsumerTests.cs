using Core.OS.Connections.Consumers;
using Core.OS.DbContext;
using Core.Shared.Connections.Commands;
using Core.Shared.Connections.Events;
using AwesomeAssertions;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Sdk.Connections.Contracts;
using Sdk.Testing.Backend;
using Xunit;

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
            ConnectionFactory.CreateDatabaseConnection(connectionString, DatabaseConnectionType.SQLite));

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
    public async Task Empty_mqtt_connection_should_publish_error()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var command = new TestConnection(Guid.NewGuid(), new Connection { Type = ConnectionType.Mqtt });

        SetupTestServiceForError(command.Connection.Id);

        // Act
        var doneEvent = await tester.TestCommand<TestConnection, TestConnectionConsumer, TestConnectionDoneEvent>(command);

        // Assert
        doneEvent.RequestId.Should().Be(command.RequestId);
        doneEvent.TestResult.Success.Should().BeFalse();
        doneEvent.TestResult.ErrorInfo.Should().NotBeNull();
    }

    [Fact]
    public async Task Mqtt_tcp_connection_should_publish_event_on_success()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var command = SetupCommandWithMqtt(MqttConnectionType.TCP);
        SetupTestServiceForSuccess(command.Connection.Id);

        // Act
        var doneEvent = await tester.TestCommand<TestConnection, TestConnectionConsumer, TestConnectionDoneEvent>(command);

        // Assert
        doneEvent.RequestId.Should().Be(command.RequestId);
        doneEvent.TestResult.Success.Should().BeTrue();
        doneEvent.TestResult.ErrorInfo.Should().BeNull();
        // var mqttConnection = command.Connection.GetMqttConnection() ?? throw new ArgumentException();
        // VerifyMqttConnect(command, mqttConnection, false);
    }

    [Fact]
    public async Task Mqtt_tcp_with_tls_connection_should_publish_event_on_success()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var command = SetupCommandWithMqtt(MqttConnectionType.TCPWithTLS);

        SetupTestServiceForSuccess(command.Connection.Id);

        // Act
        var doneEvent = await tester.TestCommand<TestConnection, TestConnectionConsumer, TestConnectionDoneEvent>(command);

        // Assert
        doneEvent.RequestId.Should().Be(command.RequestId);
        doneEvent.TestResult.Success.Should().BeTrue();
        doneEvent.TestResult.ErrorInfo.Should().BeNull();

        // var mqttConnection = command.Connection.GetMqttConnection() ?? throw new ArgumentException();
        // VerifyMqttConnect(command, mqttConnection, true);
    }

    [Fact]
    public async Task Mqtt_websocket_connection_should_publish_event_on_success()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var command = SetupCommandWithMqtt(MqttConnectionType.WebSocket);

        SetupTestServiceForSuccess(command.Connection.Id);

        // Act
        var doneEvent = await tester.TestCommand<TestConnection, TestConnectionConsumer, TestConnectionDoneEvent>(command);

        // Assert
        doneEvent.RequestId.Should().Be(command.RequestId);
        doneEvent.TestResult.Success.Should().BeTrue();
        doneEvent.TestResult.ErrorInfo.Should().BeNull();

        // var mqttConnection = command.Connection.GetMqttConnection() ?? throw new ArgumentException();
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
    public async Task Empty_database_connection_should_publish_error()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var command = new TestConnection(Guid.NewGuid(), new Connection { Type = ConnectionType.Database });
        SetupTestServiceForError(command.Connection.Id);

        // Act
        var doneEvent = await tester.TestCommand<TestConnection, TestConnectionConsumer, TestConnectionDoneEvent>(command);

        // Assert
        doneEvent.RequestId.Should().Be(command.RequestId);
        doneEvent.TestResult.Success.Should().BeFalse();
        doneEvent.TestResult.ErrorInfo.Should().NotBeNull();
    }

    [Fact]
    public async Task Sqlite_database_connection_should_publish_event_on_success()
    {
        await using var tester = new MassTransitTester(_configureServices);
        await using var context = TestDbContextFactory.CreateSqliteContext<ConnectionDbContextSqlite>();

        // Arrange
        var command = SetupCommandWithSqliteDatabase();
        SetupTestServiceForSuccess(command.Connection.Id);

        // Act
        var doneEvent = await tester.TestCommand<TestConnection, TestConnectionConsumer, TestConnectionDoneEvent>(command);

        // Assert
        doneEvent.RequestId.Should().Be(command.RequestId);
        doneEvent.TestResult.Success.Should().BeTrue();
        doneEvent.TestResult.ErrorInfo.Should().BeNull();
    }

    [Fact]
    public async Task Sqlite_database_connection_should_publish_error_on_failure()
    {
        // Arrange
        await using var tester = new MassTransitTester(_configureServices);
        var command = SetupCommandWithSqliteDatabase("DataSource=/Appdata/test.db");
        SetupTestServiceForError(command.Connection.Id);

        // Act
        var doneEvent = await tester.TestCommand<TestConnection, TestConnectionConsumer, TestConnectionDoneEvent>(command);

        // Assert
        doneEvent.RequestId.Should().Be(command.RequestId);
        doneEvent.TestResult.Success.Should().BeFalse();
        doneEvent.TestResult.ErrorInfo.Should().NotBeNull();
    }
}
