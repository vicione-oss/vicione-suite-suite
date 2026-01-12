using Blazor.Shared.Mqtt.Contracts;
using DevExpress.Blazor;

namespace Blazor.Shared.Mqtt.Helpers;

internal static class MqttTopicTreeBuilder
{
    public static bool UpdateTopicGroups(string topic, List<TopicGroup> groups)
    {
        var splitTopic = topic.Split(MqttViewerConstants.TopicSeparator);

        return SetTreeElement(splitTopic, 0, splitTopic.First(), groups);
    }
    private static bool SetTreeElement(string[] splitTopic, int index, string topicElement, List<TopicGroup> groups)
    {
        var changed = false;
        var group = groups.FirstOrDefault(g => g.Topic == topicElement);
        if (group is not null)
        {
            if (index < splitTopic.Length - 1)
                return SetTreeElement(splitTopic, index + 1, splitTopic[index + 1], group.SubTopics);
        }
        else
        {
            if (index < splitTopic.Length - 1)
            {
                group = new TopicGroup()
                {
                    Topic = topicElement,
                };
                groups.Add(group);
                groups.Sort((a, b) => string.Compare(a.Topic, b.Topic, StringComparison.Ordinal));
                changed = SetTreeElement(splitTopic, index + 1, splitTopic[index + 1], group.SubTopics);
            }
        }
        return changed;
    }

    public static void AddParentTopic(ITreeViewNodeInfo parent, List<string> topic)
    {
        if (parent.Text == MqttViewerConstants.AllTopicText)
            return;

        topic.Insert(0, parent.Text);

        AddParentTopic(parent.Parent, topic);
    }
}
