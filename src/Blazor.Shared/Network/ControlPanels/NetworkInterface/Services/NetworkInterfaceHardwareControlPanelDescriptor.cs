using Blazor.Shared.Network.ControlPanels.NetworkInterface.Components;
using Sdk.Client.ControlPanels.Services;
using ViciOne.Ui.Localization.Resources;

namespace Blazor.Shared.Network.ControlPanels.NetworkInterface.Services;

internal sealed class NetworkInterfaceHardwareControlPanelDescriptor : IControlPanelDescriptor<NetworkInterfaceHardwareControlPanel>
{
    public string Title => TechnicalTerms.Hardware;
    public Uri? IconUrl => null;
    public int? Position => 2;
    public bool ShowInNavigation => false;
}
