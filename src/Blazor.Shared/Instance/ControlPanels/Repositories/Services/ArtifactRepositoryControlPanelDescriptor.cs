using Blazor.Shared.Enums;
using Blazor.Shared.Extensions;
using Sdk.Client.ControlPanels.Services;
using ViciOne.Ui.Localization.Resources;

namespace Blazor.Shared.Instance.ControlPanels.Repositories.Services;

internal sealed class ArtifactRepositoryControlPanelDescriptor : IControlPanelDescriptor<ArtifactRepositoryControlPanel>
{
    public Uri? IconUrl => SvgIcon.ClusterOverview.GetPath();

    public string Title => CommonVocabulary.Source;

    public bool ShowInNavigation => false;
}
