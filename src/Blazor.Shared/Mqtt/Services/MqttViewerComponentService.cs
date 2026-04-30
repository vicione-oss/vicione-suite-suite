using System.Globalization;
using System.Text;
using System.Text.Json;
using Blazor.Shared.Mqtt.Contracts;
using Blazor.Shared.Mqtt.Helpers;
using MQTTnet.Protocol;
using Sdk.Connections.Contracts;
using Sdk.Connections.Extensions;

namespace Blazor.Shared.Mqtt.Services;

public sealed class MqttViewerComponentService
{
    private readonly IMqttService _mqttService;
    private MessageModel? _selectedMessage;

    public bool IsConnected => _mqttService.IsConnected;
    public string ErrorMessage { get; set; } = string.Empty;
    public List<TopicGroup> TopicGroups { get; } = [];
    public string NodeTopic { get; set; } = string.Empty;
    public string? TopicFilterText { get; private set; }
    public bool DisplayMessageDetails { get; set; }
    internal MqttFilterTypes CurrentFilter { get; private set; } = MqttFilterTypes.None;
    internal int? LastReloadInterval { get; set; }

    public event Func<Task>? PageRefreshRequested;
    public event Action? TopicGroupsChanged;
    public event Func<Task>? MqttConnected;
    public event Func<Task>? MqttDisconnected;

    public MqttViewerComponentService(IMqttService mqttService)
    {
        _mqttService = mqttService;

        EnsureAllTopicExists();
    }

    public Task OnFilterChanged(string? filter)
    {
        if (filter == TopicFilterText)
            return Task.CompletedTask;

        // all components will get filtered messages
        TopicFilterText = filter;
        return PageRefreshRequested?.Invoke() ?? Task.CompletedTask;
    }

    public Task SetNodeTopicFilter(TopicGroup topicGroup)
    {
        NodeTopic = GetTopicSubscription(topicGroup);

        return PageRefreshRequested?.Invoke() ?? Task.CompletedTask;
    }

    public Task ClearTopicTextFilter()
    {
        _mqttService.ClearMessages();
        TopicFilterText = string.Empty;

        return PageRefreshRequested?.Invoke() ?? Task.CompletedTask;
    }

    public Task ClearTopicNodeFilter()
    {
        _mqttService.ClearMessages();
        TopicGroups.First().SubTopics.Clear();
        NodeTopic = string.Empty;

        return PageRefreshRequested?.Invoke() ?? Task.CompletedTask;
    }

    public async Task ConnectClient(Connection connection)
    {
        ErrorMessage = string.Empty;

        var mqttConnection = connection.GetMqttConnection() ??
            throw new InvalidOperationException("Failed to extract mqtt connection");

        ConnectMqttEvents();

        await _mqttService.Connect(mqttConnection);
    }

    private void ConnectMqttEvents()
    {
        _mqttService.Connected += OnClientConnected;
        _mqttService.Disconnected += OnClientDisconnected;
        _mqttService.ErrorOccured += OnErrorOccured;
        _mqttService.MessageReceived += OnMessageReceived;
    }

    public async Task DisconnectClient()
    {
        await _mqttService.Disconnect();

        DisconnectMqttEvents();

        _selectedMessage = null;
        TopicFilterText = string.Empty;
        TopicGroups.Clear();
        NodeTopic = string.Empty;
    }

    private void DisconnectMqttEvents()
    {
        _mqttService.Connected -= OnClientConnected;
        _mqttService.Disconnected -= OnClientDisconnected;
        _mqttService.ErrorOccured -= OnErrorOccured;
        _mqttService.MessageReceived -= OnMessageReceived;
    }

    private void EnsureAllTopicExists()
    {
        if (TopicGroups.Any(k => k.Topic == MqttViewerConstants.AllTopicText))
            return;

        TopicGroups.Add(new TopicGroup
        {
            Topic = MqttViewerConstants.AllTopicText,
        });
    }

    public IQueryable<MessageModel?> GetMessagesByTextFilter()
        => (!string.IsNullOrEmpty(TopicFilterText)
            ? _mqttService.Messages.Where(m => m is not null && m.Topic.Contains(TopicFilterText, StringComparison.Ordinal)).AsQueryable()
            : _mqttService.Messages.Where(m => m is not null)).OrderByDescending(m => m?.MessageId).AsQueryable();

    public IQueryable<MessageModel> GetMessagesByNodeFilter()
    {
        if (string.IsNullOrEmpty(NodeTopic))
            return Array.Empty<MessageModel>().AsQueryable();

        return NodeTopic == MqttViewerConstants.AllTopicSubscription
            ? _mqttService.MessagesDict.Values.AsQueryable()
            : _mqttService.MessagesDict.Values.Where(v => v.Topic.StartsWith(NodeTopic + MqttViewerConstants.TopicSeparator, StringComparison.Ordinal)).AsQueryable();
    }

    public string GetMessageDetails()
    {
        if (_selectedMessage is null)
            return string.Empty;

        var message = _selectedMessage.Message + Environment.NewLine;

        if (_selectedMessage.Message.StartsWith('{') && _selectedMessage.Message.EndsWith('}'))
        {
            message = TryPrettifyJson(_selectedMessage.Message);
        }

        return new StringBuilder()
            .Append(CultureInfo.InvariantCulture, $"Topic: {_selectedMessage.Topic}").AppendLine()
            .Append(CultureInfo.InvariantCulture, $"Timestamp: {_selectedMessage.Timestamp:dd.MM.yyyy hh:mm:ss}").AppendLine()
            .Append(CultureInfo.InvariantCulture, $"QoS: {_selectedMessage.Qos}").AppendLine()
            .AppendLine()
            .Append(message)
            .ToString();

        static string TryPrettifyJson(string jsonString)
        {
            try
            {
                // prettify json
                using var doc = JsonDocument.Parse(
                    jsonString,
                    new JsonDocumentOptions
                    {
                        AllowTrailingCommas = true
                    }
                );
                var memoryStream = new MemoryStream();
                using (
                    var utf8JsonWriter = new Utf8JsonWriter(
                        memoryStream,
                        new JsonWriterOptions
                        {
                            Indented = true
                        }
                    )
                )
                {
                    doc.WriteTo(utf8JsonWriter);
                }
                return new UTF8Encoding().GetString(memoryStream.ToArray());
            }
            catch (Exception)
            {
            }
            return jsonString;
        }
    }

    private static string GetTopicSubscription(TopicGroup topicGroup)
    {
        if (topicGroup.Topic == MqttViewerConstants.AllTopicText)
            return MqttViewerConstants.AllTopicSubscription;

        var topicElements = new List<string>
        {
            topicGroup.Topic,
        };

        MqttTopicTreeBuilder.AddParentTopic(topicGroup.Parent, topicElements);

        return string.Join(MqttViewerConstants.TopicSeparator, [.. topicElements]);
    }

    private Task OnErrorOccured(string message)
    {
        DisconnectMqttEvents();
        ErrorMessage = message;

        return PageRefreshRequested?.Invoke() ?? Task.CompletedTask;
    }

    private async Task OnClientConnected()
    {
        EnsureAllTopicExists();

        if (_mqttService.IsConnected)
            await _mqttService.Subscribe(MqttViewerConstants.AllTopicSubscription, MqttQualityOfServiceLevel.AtMostOnce);

        if (MqttConnected is not null)
            await MqttConnected.Invoke();
    }

    private Task OnClientDisconnected()
        => MqttDisconnected?.Invoke() ?? Task.CompletedTask;

    private Task OnMessageReceived(string topic)
    {
        if (AddTopic(topic))
        {
            TopicGroupsChanged?.Invoke();

            return PageRefreshRequested?.Invoke() ?? Task.CompletedTask;
        }

        return Task.CompletedTask;
    }

    public Task ShowMessageDetails(MessageModel model)
    {
        _selectedMessage = model;
        DisplayMessageDetails = true;

        return PageRefreshRequested?.Invoke() ?? Task.CompletedTask;
    }

    public Task RequestRefresh()
        => PageRefreshRequested?.Invoke() ?? Task.CompletedTask;

    public Task CloseMessageDetails()
    {
        DisplayMessageDetails = false;
        return PageRefreshRequested?.Invoke() ?? Task.CompletedTask;
    }

    internal Task ToggleCurrentFilter(MqttFilterTypes filter)
    {
        CurrentFilter = CurrentFilter != filter ? filter : MqttFilterTypes.None;
        return PageRefreshRequested?.Invoke() ?? Task.CompletedTask;
    }

    private bool AddTopic(string topic)
    {
        var allTopic = TopicGroups.First();
        if (MqttTopicTreeBuilder.UpdateTopicGroups(topic, allTopic.SubTopics, allTopic))
            return true;

        return false;
    }
}
