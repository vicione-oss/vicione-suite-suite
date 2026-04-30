using Blazor.Shared.Mqtt.Contracts;
using Blazor.Shared.Mqtt.Enums;
using Blazor.Shared.Mqtt.Services;
using Microsoft.AspNetCore.Components;
using ViciOne.Ui.MonochromeIcons.Core.Enums;
using ViciOne.Ui.MonochromeIcons.Core.Extensions;
using ViciOne.Ui.TreeEditor.Builder;

namespace Blazor.Shared.Mqtt;

public sealed partial class MqttFilterComponent : IDisposable
{
    private static readonly string _iconCssClass = MonochromeIconName.CloseMedium.GetCssClasses(MonochromeIconSize.Small).ToSpaceSeparated();

    private readonly string _filterIconCssClass =
        MonochromeIconName.FilterLight.GetCssClasses(MonochromeIconSize.Medium).ToSpaceSeparated();
    private readonly string _fileTreeIconCssClass =
        MonochromeIconName.DataflowSolid.GetCssClasses(MonochromeIconSize.Medium).ToSpaceSeparated();
    private readonly string _testTubeSolidIconCssClass =
        MonochromeIconName.TestTubeSolid.GetCssClasses(MonochromeIconSize.Medium).ToSpaceSeparated();

    private SectionId _activeSectionId;

    [Inject]
    private MqttViewerComponentService ViewerService { get; set; } = default!;

    [Inject(Key = typeof(MqttTopicTreeServiceKey))]
    private ITreeBuilder TreeBuilder { get; set; } = default!;

    [Inject]
    private MqttTopicTreeAdapter TreeAdapter { get; set; } = default!;

    protected override Task OnInitializedAsync()
    {
        ViewerService.PageRefreshRequested += OnPageRefreshRequested;
        ViewerService.TopicGroupsChanged += OnTopicGroupsChanged;

        TreeBuilder.SetAdapter(TreeAdapter);

        return Task.CompletedTask;
    }

    private void OnTopicGroupsChanged()
        => TreeBuilder.Notifications.NotifyRootNodesChanged();

    private async Task OnPageRefreshRequested()
        => await InvokeAsync(StateHasChanged);

    private Task ToggleTopicNodeFilterArea()
        => ViewerService.ToggleCurrentFilter(MqttFilterTypes.TopicNode);

    private Task ToggleTopicTextFilter()
        => ViewerService.ToggleCurrentFilter(MqttFilterTypes.TopicText);

    private void ActiveSectionIdChanged()
    {
        if (_activeSectionId == SectionId.Filter)
            ToggleTopicTextFilter();

        if (_activeSectionId == SectionId.FileTree)
            ToggleTopicNodeFilterArea();
    }

    public void Dispose()
    {
        ViewerService.PageRefreshRequested -= OnPageRefreshRequested;
        ViewerService.TopicGroupsChanged -= OnTopicGroupsChanged;

        TreeBuilder.Dispose();
    }
}
