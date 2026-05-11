using Blazor.Shared.Mqtt.Contracts;
using Blazor.Shared.Mqtt.Services;
using AwesomeAssertions;
using MQTTnet.Protocol;
using Sdk.Connections.Contracts;
using Xunit;

namespace Blazor.Shared.Tests.Mqtt.Services;

public class MqttServiceTests
{
    public class Connect
    {
        [Fact]
        public async Task Throws_invalid_enum_argument_exception_when_protocol_is_unknown()
        {
            // Arrange
            const int protocolType = 3;
            await using var service = new MqttService();
            var connection = new MqttConnection()
            {
                Address = "localhost",
                Password = "password",
                Username = "username",
                Port = 1883,
                Protocol = (MqttConnectionType)protocolType,
            };
            var errorMessage = string.Empty;

            service.ErrorOccurred += m =>
            {
                errorMessage = m;
                return Task.CompletedTask;
            };

            // Act
            await service.Connect(connection);

            // Assert
            errorMessage.Should().Contain($"{protocolType}").And.Contain("Protocol").And.Contain("MqttConnectionType");
        }

        [Fact]
        public async Task Invokes_event_with_tcp_connection()
        {
            // Arrange
            await using var service = new MqttService();
            var connection = new MqttConnection()
            {
                Address = "http://123.123.123.123",
                Password = "password",
                Username = "username",
                Port = 1883,
                Protocol = MqttConnectionType.TCP,
            };
            var errorMessage = string.Empty;

            service.ErrorOccurred += m =>
            {
                errorMessage = m;
                return Task.CompletedTask;
            };

            // Act
            await service.Connect(connection);

            // Assert
            errorMessage.Should().NotBeNullOrEmpty();
        }

        [Fact]
        public async Task Invokes_event_with_tcp_connection_with_tls()
        {
            // Arrange
            await using var service = new MqttService();
            var connection = new MqttConnection()
            {
                Address = "https://123.123.123.123",
                Port = 1883,
                Protocol = MqttConnectionType.TCPWithTLS,
            };
            var errorMessage = string.Empty;

            service.ErrorOccurred += m =>
            {
                errorMessage = m;
                return Task.CompletedTask;
            };

            // Act
            await service.Connect(connection);

            // Assert
            errorMessage.Should().Contain("Error while connecting");
        }

        [Fact(Skip = "We can't ensure that this address is available in test environment yet")]
        public async Task Invokes_event_with_websocket_connection()
        {
            // Arrange
            await using var service = new MqttService();
            var connection = new MqttConnection()
            {
                Address = "http://www.mqtt.de",
                Port = 1883,
                Protocol = MqttConnectionType.WebSocket,
            };
            var errorMessage = string.Empty;

            service.ErrorOccurred += m =>
            {
                errorMessage = m;
                return Task.CompletedTask;
            };

            // Act
            await service.Connect(connection);

            // Assert
            errorMessage.Should().NotBeNullOrEmpty();
        }
    }

    public class Disconnect
    {
        [Fact]
        public async Task Cleans_lists()
        {
            // Arrange
            await using var service = new MqttService();

            ((MessageModel[])service.Messages)[0] = new() { Message = "test", MessageId = 1, Retained = true, Qos = MqttQualityOfServiceLevel.AtMostOnce, Topic = "test", };

            // Act
            await service.Connect(new());
            await service.Disconnect();

            // Assert
            service.MessagesDict.Should().BeEmpty();
            service.Messages.ToList().ForEach(e => e.Should().BeNull());
        }
    }

    public class ClearMessages
    {
        [Fact]
        public async Task Cleans_lists()
        {
            // Arrange
            await using var service = new MqttService();

            service.MessagesDict.Add("test", new());
            ((MessageModel[])service.Messages)[0] = new();

            // Act
            service.ClearMessages();

            // Assert
            service.MessagesDict.Should().BeEmpty();
            service.Messages.ToList().ForEach(m => m.Should().BeNull());
        }
    }

    public class Subscribe
    {
        [Fact]
        public async Task Invokes_event()
        {
            // Arrange
            await using var service = new MqttService();
            var errorMessage = string.Empty;

            service.ErrorOccurred += m =>
            {
                errorMessage = m;
                return Task.CompletedTask;
            };

            // Act
            await service.Connect(new());
            await service.Subscribe("test", MqttQualityOfServiceLevel.AtMostOnce);

            // Assert
            errorMessage.Should().NotBeNullOrEmpty();
        }
    }
}
