using Blazor.Shared.Mqtt.Contracts;
using Blazor.Shared.Mqtt.Helpers;
using Blazor.Shared.Mqtt.Services;

namespace Blazor.Shared.Mqtt.Extensions;

public static class MessageModelExtensions
{
    public static string GetRowTopic(this MessageModel messageModel, MqttViewerComponentService viewerService)
        => viewerService.NodeTopic == MqttViewerConstants.AllTopicSubscription
            ? messageModel.Topic
            : messageModel.Topic.Remove(0, viewerService.NodeTopic.Length + 1);
}
