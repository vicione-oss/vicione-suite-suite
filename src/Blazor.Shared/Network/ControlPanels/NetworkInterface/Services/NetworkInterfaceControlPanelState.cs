using Blazor.Shared.Network.ControlPanels.NetworkInterface.Models;
using Blazor.Shared.Settings.NetworkInterface.Enums;
using HostManagement.Shared.Contracts.Network;

namespace Blazor.Shared.Network.ControlPanels.NetworkInterface.Services;

public sealed class NetworkInterfaceControlPanelState : NetworkControlPanelStateBase
{
    private int _networkInterfaceIndex;
    private bool _dhcpLeaseSettingsGroupExpanded = true;

    internal string Name { get; set; } = string.Empty;
    internal bool Enabled { get; set; }
    internal IpConfigurationMode IpV4ConfigurationMode { get; set; }

    internal List<IpAddressInformation> IpAddresses { get; set; } = [];

    internal NetworkInterfaceIPv4Detail FirstIpV4Detail { get; set; } = new();
    internal List<NetworkInterfaceIPv4Detail> AdditionalIpV4Details { get; set; } = [];
    internal string? DefaultGateway { get; set; }

    internal bool MacAddressManually { get; set; }
    internal string MacAddress { get; set; } = string.Empty;
    internal string? OriginalMacAddress { get; set; }

    internal DHCPLease? DHCPLease { get; set; }
    internal bool DhcpLeaseFetching { get; set; } = true;

    internal bool VLanEnabled { get; set; }
    internal string VLanId { get; set; } = string.Empty;

    internal int NetworkInterfaceIndex
    {
        get => _networkInterfaceIndex;
        set
        {
            if (value != _networkInterfaceIndex)
            {
                _networkInterfaceIndex = value;

                OnPropertyChanged();
            }
        }
    }

    internal bool DhcpLeaseSettingsGroupExpanded
    {
        get => _dhcpLeaseSettingsGroupExpanded;
        set
        {
            if (value != _dhcpLeaseSettingsGroupExpanded)
            {
                _dhcpLeaseSettingsGroupExpanded = value;

                OnPropertyChanged();
            }
        }
    }

    internal bool HasUnsavedChanges { get; set; }

    internal bool GeneralInformationExpanded { get; set; } = true;
}
