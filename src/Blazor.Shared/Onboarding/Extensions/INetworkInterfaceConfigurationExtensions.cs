using Blazor.Shared.Onboarding.Models;
using Blazor.Shared.Settings.NetworkInterface.Enums;
using HostManagement.Shared.Contracts;
using Riok.Mapperly.Abstractions;

namespace Blazor.Shared.Onboarding.Extensions;

[Mapper]
internal static partial class INetworkInterfaceConfigurationExtensions
{
    public static partial void ApplyTo(this INetworkInterfaceConfiguration target, INetworkInterfaceConfiguration source);
}

internal static partial class INetworkInterfaceConfigurationExtensions
{
    public static void UpdateFrom(this INetworkInterfaceConfiguration target, SystemConfiguration source)
    {
        var networkInterfaces = source.NetworkInterfacesSettings.NetworkInterfaces;

        var networkInterfaceName = target.GetName();

        var networkInterface = networkInterfaces.FirstOrDefault(i => i.CommonInformation.Name == networkInterfaceName);
        if (networkInterface is null)
            return;

        if (networkInterface.IPv4.DHCPEnabled)
            target.ConfigurationMode = IpConfigurationMode.AutomaticDhcp;
        else
            target.ConfigurationMode = IpConfigurationMode.Manual;

        var ipV4Detail = networkInterface.IPv4.IPv4Details.FirstOrDefault();

        target.IpAddress = ipV4Detail?.IPAddress.ToString() ?? string.Empty;
        target.SubnetMask = ipV4Detail?.Netmask.ToString() ?? string.Empty;
        target.DefaultGateway = networkInterface.IPv4.Gateway?.ToString();
    }
}
