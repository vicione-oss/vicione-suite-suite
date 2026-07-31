using System.IO.Abstractions;
using System.Text;
using Blazor.Shared.Mqtt.Contracts;
using Core.Shared.Extensions;
using MQTTnet;
using Sdk.Connections.Contracts;

namespace Blazor.Shared.Mqtt.Services;

public sealed class MqttService : IMqttService
{
    private const int MaxMessageCount = 500;

    private bool _disposed;
    private IMqttClient? _client;
    private MessageModel?[] _messages = new MessageModel?[MaxMessageCount];

    public IEnumerable<MessageModel?> Messages => _messages;

    public Dictionary<string, MessageModel> MessagesDict { get; } = [];
    public int ReceivedMessages { get; private set; }
    public bool IsConnected => _client?.IsConnected == true;
    public event Func<Task>? Connected;
    public event Func<Task>? Disconnected;
    public event Func<string, Task>? ErrorOccurred;
    public event Func<string, Task>? MessageReceived;

    private Task HandleApplicationMessageReceived(MqttApplicationMessageReceivedEventArgs arg)
    {
        ++ReceivedMessages;

        var model = new MessageModel
        {
            Topic = arg.ApplicationMessage.Topic,
            Qos = arg.ApplicationMessage.QualityOfServiceLevel,
            Retained = arg.ApplicationMessage.Retain,
            Message = Encoding.UTF8.GetString(arg.ApplicationMessage.Payload),
            Timestamp = DateTimeOffset.UtcNow,
            MessageId = ReceivedMessages,
        };

        if (!MessagesDict.TryAdd(arg.ApplicationMessage.Topic, model))
        {
            model.MessageCount = MessagesDict[arg.ApplicationMessage.Topic].MessageCount;
            MessagesDict[arg.ApplicationMessage.Topic] = model;
        }

        MessagesDict[arg.ApplicationMessage.Topic].MessageCount++;

        var index = ReceivedMessages % MaxMessageCount;

        _messages[index] = model;

        return MessageReceived?.Invoke(arg.ApplicationMessage.Topic) ?? Task.CompletedTask;
    }

    private Task OnConnected(MqttClientConnectedEventArgs args)
        => Connected?.Invoke() ?? Task.CompletedTask;

    private Task OnDisconnected(MqttClientDisconnectedEventArgs args)
        => Disconnected?.Invoke() ?? Task.CompletedTask;

    public async Task Connect(MqttConnection mqttConnection)
    {
        try
        {
            if (_client is null)
            {
                _client = new MqttClientFactory().CreateMqttClient();
                _client.ApplicationMessageReceivedAsync += HandleApplicationMessageReceived;
                _client.ConnectedAsync += OnConnected;
                _client.DisconnectedAsync += OnDisconnected;
            }

            var builder = new MqttClientOptionsBuilder()
                .WithSuiteConnection(mqttConnection, new FileSystem());

            await _client.ConnectAsync(builder.Build());
        }
        catch (Exception e)
        {
            if (ErrorOccurred != null)
                await ErrorOccurred.Invoke(e.Message);
        }
    }

    public async Task Disconnect()
    {
        if (_client is null)
            return;

        if (_client.IsConnected)
            await _client.DisconnectAsync();

        _client.ApplicationMessageReceivedAsync -= HandleApplicationMessageReceived;
        _client.ConnectedAsync -= OnConnected;
        _client.DisconnectedAsync -= OnDisconnected;
        _client.Dispose();
        _client = null;

        ClearMessages();
        ReceivedMessages = 0;
    }

    public async Task Subscribe(string topic, MQTTnet.Protocol.MqttQualityOfServiceLevel qos)
    {
        try
        {
            await _client.SubscribeAsync(topic, qos);
        }
        catch (Exception e)
        {
            if (ErrorOccurred != null)
                await ErrorOccurred.Invoke(e.Message);
        }
    }

    public void ClearMessages()
    {
        _messages = new MessageModel[MaxMessageCount];
        MessagesDict.Clear();
    }

    public async ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            await Disconnect();
            _disposed = true;
        }

        GC.SuppressFinalize(this);
    }
}
