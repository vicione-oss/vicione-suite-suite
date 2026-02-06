using Blazor.Shared.Enums;
using Blazor.Shared.Extensions;
using Blazor.Shared.Instance.ControlPanels;
using Sdk.Client.ControlPanels.Services;

namespace Blazor.Shared.Instance.Services;

internal sealed class InstanceControlPanelDescriptor : IControlPanelDescriptor<InstanceControlPanel>
{
    public Uri IconUrl => SvgIcon.ClusterOverview.GetPath();
    public bool ShowInNavigation { get; init; }
    public string Title => ControlPanels.Localization.InstanceControlPanel.Title;
}
