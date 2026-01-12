using MQTTnet.Protocol;

namespace Blazor.Shared.Mqtt.Contracts;

public sealed class MessageModel
{
    public string Topic { get; set; } = string.Empty;
    public MqttQualityOfServiceLevel Qos { get; set; }
    public bool Retained { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public int MessageId { get; set; }
    public int MessageCount { get; set; }
}
