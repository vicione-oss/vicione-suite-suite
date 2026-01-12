using Sdk.Connections.Contracts;

namespace Core.Shared.Connections.Contracts;

public sealed class TestMqttConnectionOptions(Connection connection, MqttConnection mqttConnection, Guid requestId)
{
    public Connection Connection { get; } = connection;
    public MqttConnection MqttConnection { get; } = mqttConnection;
    public Guid RequestId { get; } = requestId;
}
