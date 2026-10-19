using Sdk.Client.ControlPanels.Services;
using ViciOne.Ui.Localization.Resources;

namespace Blazor.Shared.Connections.ControlPanels.Tags.Services;

internal sealed class ConnectionsTagsControlPanelDescriptor : IControlPanelDescriptor<ConnectionsTagsControlPanel>
{
    public Uri? IconUrl => null;
    public string Title => TechnicalTerms.TagPlural;
    public int? Position => 1;
    public bool ShowInNavigation => false;
}
