using Blazor.Shared.Mqtt.Contracts;
using Blazor.Shared.Mqtt.Enums;
using Blazor.Shared.Mqtt.Services;
using Microsoft.AspNetCore.Components;
using ViciOne.Ui.MonochromeIcons.Core.Enums;
using ViciOne.Ui.MonochromeIcons.Core.Extensions;

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

    protected override Task OnInitializedAsync()
    {
        ViewerService.PageRefreshRequested += OnPageRefreshRequested;
        return Task.CompletedTask;
    }

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
        => ViewerService.PageRefreshRequested -= OnPageRefreshRequested;
}
