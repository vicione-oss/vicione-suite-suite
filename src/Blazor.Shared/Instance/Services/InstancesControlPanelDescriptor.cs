using Blazor.Shared.Enums;
using Blazor.Shared.Extensions;
using Blazor.Shared.Instance.ControlPanels;
using Sdk.Client.ControlPanels.Services;
using ViciOne.Ui.Localization.Resources;

namespace Blazor.Shared.Instance.Services;

internal sealed class InstancesControlPanelDescriptor : IControlPanelDescriptor<InstancesControlPanel>
{
    public Uri IconUrl => SvgIcon.ClusterOverview.GetPath();
    public string Title => TechnicalTerms.InstancePlural;
}
