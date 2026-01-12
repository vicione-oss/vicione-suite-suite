using Core.OS.Connections.Mqtt;
using Sdk.Connections.Contracts;
using Sdk.Connections.Extensions;

namespace Core.OS.Connections.Extensions;

internal static class SuiteConnectionFactory
{
    public const string MqttWebsocketName = "MQTT LiveData";
    public const string MqttWebsocketDescription = "auto generated client connection (websocket)";

    public const string MqttServiceName = "MQTT Broker";
    public const string MqttServiceDescription = "auto generated service connection (tcp)";

    public static Connection? CreateDefaultMqttServiceConnection(Guid instanceId, MqttConnectionOptions options)
    {
        var mqtt = CreateMqttServiceConnection(options);
        if (mqtt is null)
            return null;

        var name = GetBetterName(instanceId, MqttServiceName);
        var desc = GetBetterDescription(instanceId, MqttServiceDescription);

        return CreateInstanceMqttConnection(instanceId, mqtt, name, desc);
    }

    public static Connection? CreateDefaultMqttWebsocketConnection(Guid instanceId, MqttConnectionOptions options)
    {
        var mqtt = CreateMqttWebsocketConnection(options);
        if (mqtt is null)
            return null;

        var name = GetBetterName(instanceId, MqttWebsocketName);
        var desc = GetBetterDescription(instanceId, MqttWebsocketDescription);

        return CreateInstanceMqttConnection(instanceId, mqtt, name, desc);
    }

    private static string GetBetterName(Guid instanceId, string prefix) => $"{prefix} ({instanceId.ToString()[..8]})";
    private static string GetBetterDescription(Guid instanceId, string prefix) => $"{prefix} for instance {instanceId}";

    public static Connection CreateInstanceMqttConnection(Guid instanceId,
        MqttConnection mqtt,
        string name,
        string description)
    {
        var connection = new Connection { Id = Guid.NewGuid() };

        connection.SetBaseProperties(name, description);
        connection.SetInstanceMetadata(instanceId, mqtt.Protocol);
        connection.SetMqttConnection(mqtt);

        return connection;
    }

    public static MqttConnection? CreateMqttServiceConnection(MqttConnectionOptions options)
        => CreateMqttConnection(options, MqttConnectionType.TCP, 1883);

    public static MqttConnection? CreateMqttWebsocketConnection(MqttConnectionOptions options)
        => CreateMqttConnection(options, MqttConnectionType.WebSocket, 9001);

    private static MqttConnection? CreateMqttConnection(MqttConnectionOptions options, MqttConnectionType protocol, int defaultPort)
    {
        if (string.IsNullOrEmpty(options.Endpoint))
            return null;

        return new MqttConnection
        {
            Address = options.Endpoint,
            Port = options.Port ?? defaultPort, // get http host from environment?
            Protocol = protocol,
            Username = options.UserName ?? string.Empty,
            Password = options.Password ?? string.Empty,
        };
    }
}
