using Blazor.Shared.Network.ControlPanels.NetworkInterface.Components;
using Sdk.Client.ControlPanels.Services;
using ViciOne.Ui.Localization.Resources;

namespace Blazor.Shared.Network.ControlPanels.NetworkInterface.Services;

internal sealed class NetworkInterfaceTcpIpControlPanelDescriptor : IControlPanelDescriptor<NetworkInterfaceTcpIpControlPanel>
{
    public string Title => TechnicalAcronyms.TcpIp;
    public Uri? IconUrl => null;
    public int? Position => 1;
    public bool ShowInNavigation => false;
}
