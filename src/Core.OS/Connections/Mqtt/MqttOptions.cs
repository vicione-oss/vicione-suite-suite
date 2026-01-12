using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;

namespace Core.OS.Connections.Mqtt;

public sealed class MqttClientOptions
{
    public const string ConfigSection = "MqttClient";

    [ValidateObjectMembers]
    public MqttConnectionOptions? WebSocketClient { get; set; }
    [ValidateObjectMembers]
    public MqttConnectionOptions? ServiceClient { get; set; }
}

public sealed class MqttConnectionOptions
{
    public string? Endpoint { get; set; }

    public string? UserName { get; set; }

    public string? Password { get; set; }

    [Range(1, 65535)]
    public int? Port { get; set; }

    public string? TopicFilter { get; set; }
}
