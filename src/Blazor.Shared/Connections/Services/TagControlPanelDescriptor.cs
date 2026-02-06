using Blazor.Shared.Connections.ControlPanels;
using Sdk.Client.ControlPanels.Services;
using ViciOne.Ui.Localization.Resources;

namespace Blazor.Shared.Connections.Services;

internal sealed class TagControlPanelDescriptor : IControlPanelDescriptor<TagControlPanel>
{
    public string Title => TechnicalTerms.Tag;
    public Uri? IconUrl => null;
    public bool ShowInNavigation => false;
}
