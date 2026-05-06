using Blazor.Shared.Network.ControlPanels.Dns.Extensions;
using Blazor.Shared.Network.ControlPanels.Dns.Models;
using Blazor.Shared.Network.ControlPanels.Dns.Services;
using ViciOne.Ui.MonochromeIcons.Core.Enums;
using ViciOne.Ui.MonochromeIcons.Core.Extensions;

namespace Blazor.Shared.Network.ControlPanels.Dns.Components;

public sealed partial class DnsControlPanel : NetworkControlPanelBase<DnsControlPanelState>
{
    private readonly string _cluster1IconCssClasses = MonochromeIconName.Cluster1.GetCssClasses().ToSpaceSeparated();

    protected override Task SystemConfigurationChanged(CancellationToken cancellationToken)
    {
        State.Initialize(SystemConfigurationService);
        return InvokeAsync(StateHasChanged);
    }

    private void AddDnsDetail()
        => State.DnsDetails.Add(new NetworkInterfaceDnsDetail());

    private void RemoveDnsDetail()
        => State.DnsDetails.RemoveAt(State.DnsDetails.Count - 1);

    private void AddSearchDomainDetail()
        => State.SearchDomainDetails.Add(new NetworkInterfaceSearchDomainDetail());

    private void RemoveSearchDomainDetail()
        => State.SearchDomainDetails.RemoveAt(State.DnsDetails.Count - 1);

    private void AddStaticHostDetail()
        => State.StaticHostDetails.Add(new NetworkInterfaceStaticHostDetail());

    private void RemoveStaticHostDetail(NetworkInterfaceStaticHostDetail staticHostDetail)
        => State.StaticHostDetails.Remove(staticHostDetail);
}
