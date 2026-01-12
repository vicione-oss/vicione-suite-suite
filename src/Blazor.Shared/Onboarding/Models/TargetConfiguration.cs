using DateAndTimeConstants = Blazor.Shared.Settings.DateAndTime.Constants;

namespace Blazor.Shared.Onboarding.Models;

internal sealed class TargetConfiguration : ITargetConfiguration
{
    public UserCredentials? UserCredentials { get; set; }

    public string Hostname { get; set; } = string.Empty;

    public TimeZoneInfo TimeZone { get; set; } = DateAndTimeConstants.DefaultTimeZone;

    public INetworkInterfaceConfiguration LocalNetwork { get; } = new NetworkInterfaceConfiguration(Constants.LocalNetworkInterfaceNameNumber);
    public INetworkInterfaceConfiguration InternetConnection { get; } = new NetworkInterfaceConfiguration(Constants.InternetNetworkInterfaceNameNumber);
}
