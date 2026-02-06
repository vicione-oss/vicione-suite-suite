using Blazor.Shared.Connections.ControlPanels;
using Sdk.Client.ControlPanels.Services;
using ViciOne.Ui.Localization.Resources;

namespace Blazor.Shared.Connections.Services;

internal sealed class ConnectionControlPanelDescriptor : IControlPanelDescriptor<ConnectionControlPanel>
{
    public string Title => TechnicalTerms.Connection;

    public Uri? IconUrl => null;

    public bool ShowInNavigation => false;
}
