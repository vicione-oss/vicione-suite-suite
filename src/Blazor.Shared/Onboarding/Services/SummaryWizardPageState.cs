using Blazor.Shared.Onboarding.Models;
using Blazor.Shared.Wizard.Services;
using HostManagement.Shared.Contracts;

namespace Blazor.Shared.Onboarding.Services;

public sealed class SummaryWizardPageState
    : WizardPageState
{
    private readonly NetworkInterfaceConfiguration _localNetwork = new(Constants.LocalNetworkInterfaceNameNumber);
    private readonly NetworkInterfaceConfiguration _internetConnection = new(Constants.InternetNetworkInterfaceNameNumber);

    internal bool DeviceExpanded { get; set; } = true;
    internal string? Hostname { get; set; }
    internal string? SerialNumber { get; set; }

    internal bool TimeZoneExpanded { get; set; } = true;
    internal string? TimeZone { get; set; }
    internal string? Date { get; set; }
    internal string? Time { get; set; }

    internal bool LocalNetworkExpanded { get; set; } = true;
    internal INetworkInterfaceConfiguration LocalNetwork => _localNetwork;

    internal bool InternetConnectionExpanded { get; set; } = true;
    internal INetworkInterfaceConfiguration InternetConnection => _internetConnection;

    internal SystemConfiguration? NewSystemConfiguration { get; set; }

    internal bool AutomaticRestartAnticipated => NewSystemConfiguration is not null;
    internal bool AutomaticRestartExpanded { get; set; } = true;
}
