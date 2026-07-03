using Blazor.Shared.Network.ControlPanels.Dns.Models;
using Blazor.Shared.Onboarding.Models;
using Sdk.Client.Wizards.Services;

namespace Blazor.Shared.Onboarding.Services;

public sealed class NetworkWizardPageState : WizardPageState
{
    private readonly NetworkInterfaceConfiguration _localNetwork = new(Constants.LocalNetworkInterfaceNameNumber);
    private readonly NetworkInterfaceConfiguration _internetConnection = new(Constants.InternetNetworkInterfaceNameNumber);
    private readonly DnsConfiguration _dns = new();

    internal bool RulesAndGuidelinesExpanded { get; set; }

    internal INetworkInterfaceConfiguration LocalNetwork => _localNetwork;
    internal INetworkInterfaceConfiguration InternetConnection => _internetConnection;
    internal IDnsConfiguration Dns => _dns;
}
