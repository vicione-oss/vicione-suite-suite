using Blazor.Shared.Enums;
using Blazor.Shared.Extensions;
using Core.Shared.HostManagement.Services;
using Sdk.Client.ControlPanels.Services;
using ViciOne.Ui.Localization.Resources;

namespace Blazor.Shared.Network.ControlPanels.NetworkInterface.Services;

internal sealed class NetworkInterfaceControlPanelDescriptor(NetworkInterfaceControlPanelState controlPanelState, ISystemConfigurationService systemConfigurationService)
    : IControlPanelDescriptor
{
    public string Category => CommonVocabulary.Network;
    public string Title => GetTitle();
    public Uri IconUrl => SvgIcon.HostConfig.GetPath();
    public int? Position => controlPanelState.NetworkInterfaceIndex;

    private string GetTitle()
    {
        var networkInterface = systemConfigurationService.SystemConfiguration.NetworkInterfacesSettings.NetworkInterfaces
            .ElementAtOrDefault(controlPanelState.NetworkInterfaceIndex);

        return networkInterface?.CommonInformation.Name ?? CommonVocabulary.Unknown;
    }
}
