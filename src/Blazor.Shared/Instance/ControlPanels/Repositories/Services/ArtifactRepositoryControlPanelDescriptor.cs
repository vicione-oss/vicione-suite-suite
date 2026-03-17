using Blazor.Shared.Enums;
using Blazor.Shared.Extensions;
using Sdk.Client.ControlPanels.Services;

namespace Blazor.Shared.Instance.ControlPanels.Repositories.Services;

internal sealed class ArtifactRepositoryControlPanelDescriptor : IControlPanelDescriptor<ArtifactRepositoryControlPanel>
{
    public Uri IconUrl => SvgIcon.ClusterOverview.GetPath();

    public string Title => Localization.ArtifactRepositoryControlPanel.Source;

    public bool ShowInNavigation => false;
}
