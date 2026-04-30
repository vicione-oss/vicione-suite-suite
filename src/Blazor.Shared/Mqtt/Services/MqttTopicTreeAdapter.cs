using Blazor.Shared.Mqtt.Contracts;
using ViciOne.Ui.MonochromeIcons.Core.Enums;
using ViciOne.Ui.MonochromeIcons.Core.Extensions;
using ViciOne.Ui.TreeEditor.Builder.Interface;
using ViciOne.Ui.TreeEditor.Builder.Interface.Icons;
using ViciOne.Ui.TreeEditor.Builder.Interface.NodeActions;
using ViciOne.Ui.TreeEditor.Builder.Interface.NodeActions.Arguments;
using ViciOne.Ui.TreeEditor.Builder.Interface.Nodes;

namespace Blazor.Shared.Mqtt.Services;

internal sealed class MqttTopicTreeAdapter : TreeAdapter
{
    private static readonly SvgIcon s_searchIcon = new(string.Empty)
    {
        CssClasses = MonochromeIconName.Search.GetCssClasses(MonochromeIconSize.Small),
    };

    private readonly MqttViewerComponentService _viewerService;
    private Action<ITreeNode>? _dblClickAction;

    public MqttTopicTreeAdapter(MqttViewerComponentService viewerService)
        => _viewerService = viewerService;

    public override void Setup()
        => Builder.Guidelines.Show = true;

    public override IEnumerable<ITreeNode> GetRootNodes()
        => _viewerService.TopicGroups;

    public override IEnumerable<ITreeNode> GetChildren(ITreeNode treeNode)
        => treeNode is TopicGroup topicGroup ? topicGroup.SubTopics : [];

    public override ITreeNode? GetParent(ITreeNode treeNode)
        => treeNode is TopicGroup topicGroup ? topicGroup.Parent : null;

    public override bool HasChildren(ITreeNode treeNode)
        => treeNode is TopicGroup topicGroup && topicGroup.SubTopics.Count > 0;

    public override string GetDisplayText(ITreeNode treeNode)
        => treeNode is TopicGroup topicGroup ? topicGroup.Topic : string.Empty;

    public override bool IsExpanded(ITreeNode treeNode)
        => treeNode is TopicGroup topicGroup && topicGroup.Expanded;

    public override IEnumerable<INodeAction> GetActions(ITreeNode treeNode)
    {
        yield return new NodeButton
        {
            Icon = s_searchIcon,
            Description = string.Empty,
            Action = OnSearchButtonClicked,
        };
    }

    public override Action<ITreeNode>? GetDblClickAction(ITreeNode treeNode)
    {
        _dblClickAction ??= OnDoubleClick;

        return _dblClickAction;
    }

    private void OnSearchButtonClicked(NodeButton button, VisibleActionArguments args)
    {
        if (args.Node is TopicGroup topicGroup)
            _viewerService.SetNodeTopicFilter(topicGroup);
    }

    private void OnDoubleClick(ITreeNode treeNode)
    {
        if (treeNode is TopicGroup topicGroup)
            Builder.Expansion.ChangeExpansion(topicGroup, !topicGroup.Expanded);
    }
}
