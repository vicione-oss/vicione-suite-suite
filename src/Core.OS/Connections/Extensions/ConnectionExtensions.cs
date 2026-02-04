using Sdk.Connections;
using Sdk.Connections.Contracts;

namespace Core.OS.Connections.Extensions;

internal static class ConnectionExtensions
{
    extension(Connection connection)
    {
        public void SetInstanceMetadata(Guid instanceId, MqttConnectionType protocol)
        {
            connection.Metadata.Add(ConnectionConstants.MetaDataKeys.InstanceId, instanceId.ToString());
            connection.Metadata.Add(ConnectionConstants.MetaDataKeys.MqttClientProtocol, Enum.GetName(protocol));
        }

        public void SetBaseProperties(string name,
            string description,
            bool managed)
        {
            connection.Name = name;
            connection.Description = description;
            connection.Managed = managed;
        }
    }
}
