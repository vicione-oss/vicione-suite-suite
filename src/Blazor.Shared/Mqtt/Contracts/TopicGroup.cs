namespace Blazor.Shared.Mqtt.Contracts
{
    public sealed class TopicGroup
    {
        public string Topic { get; set; } = string.Empty;
        public List<TopicGroup> SubTopics { get; set; } = [];
    }
}
