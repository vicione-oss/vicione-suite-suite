using System.IO.Abstractions.TestingHelpers;
using System.Text;
using Core.Shared.Extensions;
using MQTTnet;
using Sdk.Connections.Contracts;

namespace Core.Shared.Tests.Extensions;

public class MqttClientOptionsBuilderExtensionsTests
{
    private static MqttConnection CreateTcpConnection() => new()
    {
        Address = "broker.local",
        Port = 1883,
        Protocol = MqttConnectionType.TCP,
        ProtocolVersion = MqttProtocolVersion.V500,
    };

    private static MqttClientOptions Build(MqttConnection connection) =>
        new MqttClientOptionsBuilder()
            .WithSuiteConnection(connection, new MockFileSystem())
            .Build();

    public sealed class WithCredentials : MqttClientOptionsBuilderExtensionsTests
    {
        [Fact]
        public void Should_apply_username_and_password_when_both_are_provided()
        {
            // Arrange
            var connection = CreateTcpConnection();
            connection.Username = "user";
            connection.Password = "secret";

            // Act
            var options = Build(connection);

            // Assert
            options.Credentials.Should().NotBeNull();
            options.Credentials!.GetUserName(options).Should().Be("user");
            Encoding.UTF8.GetString(options.Credentials.GetPassword(options)).Should().Be("secret");
        }

        [Fact]
        public void Should_apply_username_only_when_password_is_empty()
        {
            // Arrange
            var connection = CreateTcpConnection();
            connection.Username = "user";
            connection.Password = string.Empty;

            // Act
            var options = Build(connection);

            // Assert
            options.Credentials.Should().NotBeNull();
            options.Credentials!.GetUserName(options).Should().Be("user");
        }

        [Fact]
        public void Should_not_apply_credentials_when_username_is_empty()
        {
            // Arrange
            var connection = CreateTcpConnection();
            connection.Username = string.Empty;

            // Act
            var options = Build(connection);

            // Assert
            options.Credentials.Should().BeNull();
        }
    }

    public sealed class WithClientId : MqttClientOptionsBuilderExtensionsTests
    {
        [Fact]
        public void Should_apply_client_id_when_provided()
        {
            // Arrange
            var connection = CreateTcpConnection();
            connection.ClientId = "my-client";

            // Act
            var options = Build(connection);

            // Assert
            options.ClientId.Should().Be("my-client");
        }
    }

    public sealed class WithProtocolAndVersion : MqttClientOptionsBuilderExtensionsTests
    {
        [Fact]
        public void Should_configure_tcp_server_for_tcp_protocol()
        {
            // Arrange
            var connection = CreateTcpConnection();

            // Act
            var options = Build(connection);

            // Assert
            var tcpOptions = options.ChannelOptions.Should().BeOfType<MqttClientTcpOptions>().Subject;
            tcpOptions.RemoteEndpoint.ToString().Should().Contain("broker.local");
        }

        [Fact]
        public void Should_configure_websocket_server_for_websocket_protocol()
        {
            // Arrange
            var connection = CreateTcpConnection();
            connection.Protocol = MqttConnectionType.WebSocket;

            // Act
            var options = Build(connection);

            // Assert
            options.ChannelOptions.Should().BeOfType<MqttClientWebSocketOptions>();
        }

        [Fact]
        public void Should_map_protocol_version()
        {
            // Arrange
            var connection = CreateTcpConnection();
            connection.ProtocolVersion = MqttProtocolVersion.V311;

            // Act
            var options = Build(connection);

            // Assert
            options.ProtocolVersion.Should().Be(MQTTnet.Formatter.MqttProtocolVersion.V311);
        }
    }

    public sealed class WithCleanSessionSettings : MqttClientOptionsBuilderExtensionsTests
    {
        [Fact]
        public void Should_apply_clean_session_flag()
        {
            // Arrange
            var connection = CreateTcpConnection();
            connection.CleanSession = false;

            // Act
            var options = Build(connection);

            // Assert
            options.CleanSession.Should().BeFalse();
        }
    }

    public sealed class WithWillOptions : MqttClientOptionsBuilderExtensionsTests
    {
        [Fact]
        public void Should_apply_will_options_when_topic_and_message_are_provided()
        {
            // Arrange
            var connection = CreateTcpConnection();
            connection.WillTopic = "status/offline";
            connection.WillMessage = "offline";
            connection.WillRetain = true;
            connection.QualityOfService = MqttQualityOfServiceLevel.AtLeastOnce;

            // Act
            var options = Build(connection);

            // Assert
            options.WillTopic.Should().Be("status/offline");
            Encoding.UTF8.GetString(options.WillPayload).Should().Be("offline");
            options.WillRetain.Should().BeTrue();
            options.WillQualityOfServiceLevel.Should().Be(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce);
        }

        [Fact]
        public void Should_not_apply_will_options_when_topic_is_missing()
        {
            // Arrange
            var connection = CreateTcpConnection();
            connection.WillMessage = "offline";

            // Act
            var options = Build(connection);

            // Assert
            options.WillTopic.Should().BeNull();
        }
    }
}
