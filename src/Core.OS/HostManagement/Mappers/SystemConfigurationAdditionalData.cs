using System.Net.NetworkInformation;
using HostManagement.Shared.Contracts.Network;

namespace Core.OS.HostManagement.Mappers;

/// <summary>
/// HostManagement data the Suite format needs beyond the system configuration itself.
/// </summary>
/// <param name="DhcpLeases">Leases by interface name, for interfaces with DHCP enabled.</param>
/// <param name="OriginalPhysicalAddresses">Factory MAC addresses by interface name, for interfaces without a user-defined one.</param>
/// <param name="NtpFallbackServers">The NTP servers used when none are configured.</param>
public sealed record SystemConfigurationAdditionalData(
    Dictionary<string, DHCPLease?> DhcpLeases,
    Dictionary<string, PhysicalAddress?> OriginalPhysicalAddresses,
    List<string> NtpFallbackServers);
