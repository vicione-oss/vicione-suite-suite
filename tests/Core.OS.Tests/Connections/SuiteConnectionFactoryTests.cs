using AwesomeAssertions;
using Core.OS.Connections.Extensions;
using Core.OS.Connections.Mqtt;
using Sdk.Connections;
using Sdk.Connections.Contracts;
using Sdk.Connections.Extensions;
using Xunit;

namespace Core.OS.Tests.Connections;

public class SuiteConnectionFactoryTests
{
    public sealed class CreateDefaultMqttServiceConnection : SuiteConnectionFactoryTests
    {
        [Fact]
        public void Should_return_null_when_endpoint_is_missing()
        {
            // Arrange
            var options = new MqttConnectionOptions();

            // Act
            var result = SuiteConnectionFactory.CreateDefaultMqttServiceConnection(Guid.NewGuid(), options);

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public void Should_create_connection_with_expected_properties()
        {
            // Arrange
            var instanceId = Guid.NewGuid();
            var options = new MqttConnectionOptions { Endpoint = "mqtt.local" };

            // Act
            var result = SuiteConnectionFactory.CreateDefaultMqttServiceConnection(instanceId, options);

            // Assert
            result.Should().NotBeNull();
            result!.Name.Should().StartWith(SuiteConnectionFactory.MqttServiceName);
            result.Description.Should().Contain(instanceId.ToString());
            result.Metadata.Should().ContainKey(ConnectionConstants.MetaDataKeys.InstanceId);
            result.Metadata.Should().ContainKey(ConnectionConstants.MetaDataKeys.MqttClientProtocol);
        }
    }

    public sealed class CreateDefaultMqttWebsocketConnection : SuiteConnectionFactoryTests
    {
        [Fact]
        public void Should_return_null_when_endpoint_is_missing()
        {
            // Arrange
            var options = new MqttConnectionOptions();

            // Act
            var result = SuiteConnectionFactory.CreateDefaultMqttWebsocketConnection(Guid.NewGuid(), options);

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public void Should_create_connection_with_expected_properties()
        {
            // Arrange
            var instanceId = Guid.NewGuid();
            var options = new MqttConnectionOptions { Endpoint = "mqtt-ws.local" };

            // Act
            var result = SuiteConnectionFactory.CreateDefaultMqttWebsocketConnection(instanceId, options);

            // Assert
            result.Should().NotBeNull();
            result!.Name.Should().StartWith(SuiteConnectionFactory.MqttWebsocketName);
            result.Description.Should().Contain(instanceId.ToString());
            result.Metadata.Should().ContainKey(ConnectionConstants.MetaDataKeys.InstanceId);
            result.Metadata.Should().ContainKey(ConnectionConstants.MetaDataKeys.MqttClientProtocol);
        }
    }

    public sealed class CreateInstanceMqttConnection : SuiteConnectionFactoryTests
    {
        [Fact]
        public void Should_create_connection_with_given_properties_and_mqtt_values()
        {
            // Arrange
            var instanceId = Guid.NewGuid();
            var mqtt = new MqttConnection
            {
                Address = "broker.local",
                Port = 1234,
                Protocol = MqttConnectionType.WebSocket,
                Username = "user",
                Password = "pw"
            };

            // Act
            var result = SuiteConnectionFactory.CreateInstanceMqttConnection(
                instanceId, mqtt, "myName", "myDesc");

            // Assert
            result.Should().NotBeNull();
            result.Name.Should().Be("myName");
            result.Description.Should().Be("myDesc");
            result.Metadata.Should().ContainKey(ConnectionConstants.MetaDataKeys.InstanceId);
            result.Metadata.Should().ContainKey(ConnectionConstants.MetaDataKeys.MqttClientProtocol);
            result.GetMqttConnection().Should().Be(mqtt);
        }
    }

    public sealed class CreateMqttServiceConnection : SuiteConnectionFactoryTests
    {
        [Fact]
        public void Should_return_null_if_endpoint_is_empty()
        {
            // Arrange
            var options = new MqttConnectionOptions();

            // Act
            var result = SuiteConnectionFactory.CreateMqttServiceConnection(options);

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public void Should_use_default_port_1883_when_not_specified()
        {
            // Arrange
            var options = new MqttConnectionOptions { Endpoint = "broker.local" };

            // Act
            var result = SuiteConnectionFactory.CreateMqttServiceConnection(options);

            // Assert
            result.Should().NotBeNull();
            result!.Port.Should().Be(1883);
            result.Protocol.Should().Be(MqttConnectionType.TCP);
        }
    }

    public sealed class CreateMqttWebsocketConnection : SuiteConnectionFactoryTests
    {
        [Fact]
        public void Should_use_default_port_9001_when_not_specified()
        {
            // Arrange
            var options = new MqttConnectionOptions { Endpoint = "broker.local" };

            // Act
            var result = SuiteConnectionFactory.CreateMqttWebsocketConnection(options);

            // Assert
            result.Should().NotBeNull();
            result!.Port.Should().Be(9001);
            result.Protocol.Should().Be(MqttConnectionType.WebSocket);
        }
    }
}
