using Blazor.Shared.Settings.NetworkInterface.Enums;

namespace Blazor.Shared.Onboarding.Models;

public interface INetworkInterfaceConfiguration : IHasNetworkInterfaceNumber
{
    IpConfigurationMode ConfigurationMode { get; set; }

    string IpAddress { get; set; }
    string SubnetMask { get; set; }
    string? DefaultGateway { get; set; }
    string? DnsServer { get; set; }
}
