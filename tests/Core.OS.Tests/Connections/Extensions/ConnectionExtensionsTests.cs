using AwesomeAssertions;
using Core.OS.Connections.Extensions;
using Sdk.Connections;
using Sdk.Connections.Contracts;
using Xunit;

namespace Core.OS.Tests.Connections.Extensions;

public class ConnectionExtensionsTests
{
    public sealed class SetInstanceMetadata
    {
        [Fact]
        public void Should_add_instance_id_and_protocol_to_metadata()
        {
            // Arrange
            var connection = new Connection();
            var instanceId = Guid.NewGuid();
            var protocol = MqttConnectionType.TCPWithTLS;

            // Act
            connection.SetInstanceMetadata(instanceId, protocol);

            // Assert
            connection.Metadata.Should().ContainKey(ConnectionConstants.MetaDataKeys.InstanceId);
            connection.Metadata[ConnectionConstants.MetaDataKeys.InstanceId].Should().Be(instanceId.ToString());

            connection.Metadata.Should().ContainKey(ConnectionConstants.MetaDataKeys.MqttClientProtocol);
            connection.Metadata[ConnectionConstants.MetaDataKeys.MqttClientProtocol].Should().Be(protocol.ToString());
        }
    }

    public sealed class SetBaseProperties
    {
        [Fact]
        public void Should_set_name_and_description()
        {
            // Arrange
            var connection = new Connection();

            // Act
            connection.SetBaseProperties("MyConn", "This is a connection", false);

            // Assert
            connection.Name.Should().Be("MyConn");
            connection.Description.Should().Be("This is a connection");
        }
    }
}
