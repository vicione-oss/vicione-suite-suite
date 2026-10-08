using System.Net;
using System.Net.NetworkInformation;
using HostManagement.Shared.Contracts;
using HostManagement.Shared.Contracts.Network;
using Riok.Mapperly.Abstractions;

namespace Core.Shared.HostManagement.Extensions;

[Mapper(UseDeepCloning = true, IgnoreObsoleteMembersStrategy = IgnoreObsoleteMembersStrategy.Both)]
public static partial class SystemConfigurationExtensions
{
    public static partial SystemConfiguration Clone(this SystemConfiguration source);

    // Mapperly refills getter-only lists in place, which empties them when source and target share the instance, so ApplyTo copies them.
    [MapperIgnoreSource(nameof(SystemConfiguration.Version))]
    [MapperIgnoreSource(nameof(SystemConfiguration.NetworkInterfaces))]
    [MapperIgnoreSource(nameof(SystemConfiguration.Services))]
    private static partial void ApplyToInternal(this SystemConfiguration source, SystemConfiguration target);

    private static partial List<NetworkInterfaceDetail> CloneNetworkInterfaces(List<NetworkInterfaceDetail> networkInterfaces);

    private static IPAddress IPAddressToIPAddress(IPAddress ipAddress) => IPAddress.Parse(ipAddress.ToString());

    private static PhysicalAddress PhysicalAddressToPhysicalAddress(PhysicalAddress physicalAddress) => new(physicalAddress.GetAddressBytes());
}

public static partial class SystemConfigurationExtensions
{
    public static void ApplyTo(this SystemConfiguration source, SystemConfiguration target)
    {
        source.ApplyToInternal(target);

        var networkInterfaces = CloneNetworkInterfaces(source.NetworkInterfaces);

        target.NetworkInterfaces.Clear();
        target.NetworkInterfaces.AddRange(networkInterfaces);

        var services = source.Services == target.Services ? [.. source.Services] : source.Services;

        target.Services.Clear();
        target.Services.AddRange(services);
    }
}
