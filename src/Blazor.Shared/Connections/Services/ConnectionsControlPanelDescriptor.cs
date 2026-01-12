using Blazor.Shared.Connections.ControlPanels;
using Sdk.Client.ControlPanels.Services;
using ViciOne.Ui.Localization.Resources;

namespace Blazor.Shared.Connections.Services;

internal sealed class ConnectionsControlPanelDescriptor : IControlPanelDescriptor<ConnectionsControlPanel>
{
    public string IconPath => string.Empty;
    public string Title => TechnicalTerms.ConnectionPlural;
}
