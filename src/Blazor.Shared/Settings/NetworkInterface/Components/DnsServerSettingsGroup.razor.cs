using Blazor.Shared.Network.ControlPanels.Dns.Models;
using Microsoft.AspNetCore.Components;

namespace Blazor.Shared.Settings.NetworkInterface.Components;

public sealed partial class DnsServerSettingsGroup : ComponentBase
{
    [Parameter] public bool Expanded { get; set; }
    [Parameter] public EventCallback<bool> ExpandedChanged { get; set; }
    [Parameter, EditorRequired] public List<NetworkInterfaceDnsDetail> Details { get; set; } = [];
    [Parameter] public EventCallback Changed { get; set; }

    private void AddDnsDetail()
        => Details.Add(new NetworkInterfaceDnsDetail());

    private void RemoveDnsDetail()
        => Details.RemoveAt(Details.Count - 1);

    private async Task NotifyExpandedChanged()
    {
        if (ExpandedChanged.HasDelegate)
            await ExpandedChanged.InvokeAsync(Expanded);

        await NotifyChanged();
    }

    private async Task NotifyChanged()
    {
        if (Changed.HasDelegate)
            await Changed.InvokeAsync();
    }
}
