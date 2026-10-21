using Blazor.Shared.Network.ControlPanels.NetworkInterface.Models;
using Blazor.Shared.Settings.NetworkInterface.Enums;
using HostManagement.Shared.Contracts.Network;
using ViciOne.SourceGenerators.NotifyChanged;

namespace Blazor.Shared.Network.ControlPanels.NetworkInterface.Services;

public sealed partial class NetworkInterfaceControlPanelState : NetworkControlPanelStateBase
{
    internal string Name { get; set; } = string.Empty;
    internal bool Enabled { get; set; }
    internal IpConfigurationMode IpV4ConfigurationMode { get; set; }

    internal NetworkInterfaceIPv4Detail FirstIpV4Detail { get; set; } = new();
    internal List<NetworkInterfaceIPv4Detail> AdditionalIpV4Details { get; set; } = [];
    internal string? DefaultGateway { get; set; }

    internal bool MacAddressManually { get; set; }
    internal string MacAddress { get; set; } = string.Empty;
    internal string? OriginalPhysicalAddress { get; set; }

    internal DHCPLease? DHCPLease { get; set; }
    internal bool DhcpLeaseFetching { get; set; } = true;

    internal bool VLanEnabled { get; set; }
    internal string VLanId { get; set; } = string.Empty;

    [NotifyChanged]
    internal partial int NetworkInterfaceIndex { get; set; }

    [NotifyChanged]
    internal partial bool DhcpLeaseSettingsGroupExpanded { get; set; } = true;

    internal bool GeneralHasChanges { get; set; }
    internal bool TcpIpHasChanges { get; set; }
    internal bool HardwareHasChanges { get; set; }

    internal bool HasUnsavedChanges => GeneralHasChanges || TcpIpHasChanges || HardwareHasChanges;

    internal bool GeneralInformationExpanded { get; set; } = true;
}
