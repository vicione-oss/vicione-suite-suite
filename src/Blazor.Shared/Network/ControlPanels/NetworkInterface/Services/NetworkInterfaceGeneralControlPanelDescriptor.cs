using Blazor.Shared.Network.ControlPanels.NetworkInterface.Components;
using Sdk.Client.ControlPanels.Services;
using ViciOne.Ui.Localization.Resources;

namespace Blazor.Shared.Network.ControlPanels.NetworkInterface.Services;

internal sealed class NetworkInterfaceGeneralControlPanelDescriptor : IControlPanelDescriptor<NetworkInterfaceGeneralControlPanel>
{
    public string Title => CommonVocabulary.General;
    public Uri? IconUrl => null;
    public int? Position => 0;
    public bool ShowInNavigation => false;
}
