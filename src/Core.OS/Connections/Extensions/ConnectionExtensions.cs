using Sdk.Connections;
using Sdk.Connections.Contracts;

namespace Core.OS.Connections.Extensions;

internal static class ConnectionExtensions
{
    public static void SetInstanceMetadata(this Connection connection, Guid instanceId, MqttConnectionType protocol)
    {
        connection.Metadata.Add(ConnectionConstants.MetaDataKeys.InstanceId, instanceId.ToString());
        connection.Metadata.Add(ConnectionConstants.MetaDataKeys.MqttClientProtocol, Enum.GetName(protocol));
    }

    public static void SetBaseProperties(this Connection connection,
        string name,
        string description,
        bool managed)
    {
        connection.Name = name;
        connection.Description = description;
        connection.Managed = managed;
    }
}
