namespace Blazor.Shared.Onboarding.Models;

internal interface ITargetConfiguration
{
    UserCredentials? UserCredentials { get; set; }

    string Hostname { get; set; }

    TimeZoneInfo TimeZone { get; set; }

    INetworkInterfaceConfiguration LocalNetwork { get; }
    INetworkInterfaceConfiguration InternetConnection { get; }
}
