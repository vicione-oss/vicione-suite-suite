using Blazor.Shared.Mqtt.Contracts;
using Sdk.Connections.Contracts;

namespace Blazor.Shared.Mqtt.Services;

public interface IMqttService : IAsyncDisposable
{
    IEnumerable<MessageModel?> Messages { get; }
    Dictionary<string, MessageModel> MessagesDict { get; }
    int ReceivedMessages { get; }
    bool IsConnected { get; }
    event Func<Task>? Connected;
    event Func<Task>? Disconnected;
    event Func<string, Task>? ErrorOccurred;
    event Func<string, Task>? MessageReceived;

    Task Connect(MqttConnection mqttConnection);
    Task Disconnect();
    Task Subscribe(string topic, MQTTnet.Protocol.MqttQualityOfServiceLevel qos);
    void ClearMessages();
}
