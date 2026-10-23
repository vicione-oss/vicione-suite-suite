using Blazor.Shared.Network.ControlPanels.RemoteAccess.Components;
using Blazor.Shared.Network.Services;
using Sdk.Client.ControlPanels.Services;
using ViciOne.Ui.Localization.Resources;

namespace Blazor.Shared.Network.ControlPanels.RemoteAccess.Services;

internal sealed class RemoteAccessControlPanelDescriptor : IControlPanelDescriptor<RemoteAccessControlPanel>
{
    public string Title => TechnicalTerms.RemoteAccess;
    public int? Position => ControlPanelNetworkCategoryDescriptor.FirstPositionAfterNetworkInterfaces + 3;
    public Uri? IconUrl => null;
}
