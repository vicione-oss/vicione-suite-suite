using Blazor.Shared.Settings.NetworkInterface.Enums;

namespace Blazor.Shared.Onboarding.Models;

internal sealed class NetworkInterfaceConfiguration(int networkInterfaceNumber) : INetworkInterfaceConfiguration
{
    public int Number => networkInterfaceNumber;

    public IpConfigurationMode ConfigurationMode { get; set; }

    public string IpAddress { get; set; } = string.Empty;
    public string SubnetMask { get; set; } = string.Empty;
    public string? DefaultGateway { get; set; }
}
