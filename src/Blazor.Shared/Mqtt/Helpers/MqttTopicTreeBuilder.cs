using Blazor.Shared.Mqtt.Contracts;

namespace Blazor.Shared.Mqtt.Helpers;

internal static class MqttTopicTreeBuilder
{
    public static bool UpdateTopicGroups(string topic, List<TopicGroup> groups, TopicGroup? parent = null)
    {
        var splitTopic = topic.Split(MqttViewerConstants.TopicSeparator);

        return SetTreeElement(splitTopic, 0, splitTopic.First(), groups, parent);
    }
    private static bool SetTreeElement(string[] splitTopic, int index, string topicElement, List<TopicGroup> groups, TopicGroup? parent)
    {
        var changed = false;

        var group = groups.FirstOrDefault(g => g.Topic == topicElement);
        if (group is not null)
        {
            if (index < splitTopic.Length - 1)
                return SetTreeElement(splitTopic, index + 1, splitTopic[index + 1], group.SubTopics, group);
        }
        else
        {
            if (index < splitTopic.Length - 1)
            {
                group = new TopicGroup()
                {
                    Topic = topicElement,
                    Parent = parent,
                };
                groups.Add(group);
                groups.Sort((a, b) => string.Compare(a.Topic, b.Topic, StringComparison.Ordinal));

                SetTreeElement(splitTopic, index + 1, splitTopic[index + 1], group.SubTopics, group);

                changed = true;
            }
        }
        return changed;
    }

    public static void AddParentTopic(TopicGroup? parent, List<string> topic)
    {
        if (parent is null || parent.Topic == MqttViewerConstants.AllTopicText)
            return;

        topic.Insert(0, parent.Topic);

        AddParentTopic(parent.Parent, topic);
    }
}
