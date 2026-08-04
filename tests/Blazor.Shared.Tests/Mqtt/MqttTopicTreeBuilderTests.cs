using Blazor.Shared.Mqtt.Contracts;
using Blazor.Shared.Mqtt.Helpers;

namespace Blazor.Shared.Tests.Mqtt;

public sealed class MqttTopicTreeBuilderTests
{
    private const string Topic1 = "topic1";
    private const string SubTopic1 = $"{Topic1}/subTopic1";
    private const string SubSubTopic1 = $"{SubTopic1}/subSubTopic1";
    private const string Topic2 = "topic2";
    private const string SubTopic2 = $"{Topic2}/subTopic2";
    private const string SubSubTopic2 = $"{SubTopic2}/subSubTopic2";

    [Fact]
    public void Should_generate_topic_groups()
    {
        // Arrange
        var allTopic = new TopicGroup
        {
            Topic = "All",
        };

        // Act
        MqttTopicTreeBuilder.UpdateTopicGroups(SubSubTopic2, allTopic.SubTopics);
        MqttTopicTreeBuilder.UpdateTopicGroups(SubSubTopic1, allTopic.SubTopics);

        // Assert
        allTopic.SubTopics.Should().HaveCount(2);
        allTopic.SubTopics[0].Topic.Should().Be(Topic1);
        allTopic.SubTopics[1].Topic.Should().Be(Topic2);

        allTopic.SubTopics[0].SubTopics.Should().HaveCount(1);
        allTopic.SubTopics[1].SubTopics.Should().HaveCount(1);

        SubTopic1.Should().EndWith(allTopic.SubTopics[0].SubTopics[0].Topic);
        SubTopic2.Should().EndWith(allTopic.SubTopics[1].SubTopics[0].Topic);
    }

    [Fact]
    public void Should_not_extend_topics_when_called_multiple_times()
    {
        // Arrange
        var allTopic = new TopicGroup
        {
            Topic = "All",
        };

        // Act
        MqttTopicTreeBuilder.UpdateTopicGroups(Topic2, allTopic.SubTopics);
        MqttTopicTreeBuilder.UpdateTopicGroups(SubTopic2, allTopic.SubTopics);
        MqttTopicTreeBuilder.UpdateTopicGroups(SubSubTopic2, allTopic.SubTopics);

        MqttTopicTreeBuilder.UpdateTopicGroups(Topic2, allTopic.SubTopics);
        MqttTopicTreeBuilder.UpdateTopicGroups(SubTopic2, allTopic.SubTopics);
        MqttTopicTreeBuilder.UpdateTopicGroups(SubSubTopic2, allTopic.SubTopics);

        // Assert
        allTopic.SubTopics.Should().HaveCount(1);
        allTopic.SubTopics[0].Topic.Should().Be(Topic2);
        allTopic.SubTopics[0].SubTopics.Should().HaveCount(1);
    }
}
