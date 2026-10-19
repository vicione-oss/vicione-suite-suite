using Sdk.Client.ControlPanels.Services;
using ViciOne.Ui.Localization.Resources;

namespace Blazor.Shared.Connections.ControlPanels.Connections.Services;

internal sealed class ConnectionsControlPanelDescriptor
    : IControlPanelDescriptor<ConnectionsControlPanel>, IControlPanelParentDescriptor
{
    public Uri? IconUrl => null;
    public string Title => TechnicalTerms.ConnectionPlural;
    public int? Position => 1;
}
