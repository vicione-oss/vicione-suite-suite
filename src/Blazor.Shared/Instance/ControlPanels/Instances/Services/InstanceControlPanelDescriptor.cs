using Blazor.Shared.Enums;
using Blazor.Shared.Extensions;
using Sdk.Client.ControlPanels.Services;

namespace Blazor.Shared.Instance.ControlPanels.Instances.Services;

internal sealed class InstanceControlPanelDescriptor : IControlPanelDescriptor<InstanceControlPanel>
{
    public Uri IconUrl => SvgIcon.ClusterOverview.GetPath();
    public bool ShowInNavigation { get; init; }
    public string Title => Localization.InstanceControlPanel.Title;
}
