using ViciOne.Ui.TreeEditor.Builder.Interface.NodeIdentifier;
using ViciOne.Ui.TreeEditor.Builder.Interface.Nodes;

namespace Blazor.Shared.Mqtt.Contracts;

public sealed class TopicGroup : ITreeNode
{
    private GuidNodeIdentifier? _id;

    public string Topic { get; set; } = string.Empty;
    public List<TopicGroup> SubTopics { get; set; } = [];
    public TopicGroup? Parent { get; set; }
    public bool Expanded { get; set; }

    public Guid Id
    {
        get => GetGuidNodeIdentifier().Value;
        set => _id = new GuidNodeIdentifier { Value = value };
    }

    INodeIdentifier ITreeNode.Id => GetGuidNodeIdentifier();

    private GuidNodeIdentifier GetGuidNodeIdentifier()
        => _id ??= GuidNodeIdentifier.New();
}
