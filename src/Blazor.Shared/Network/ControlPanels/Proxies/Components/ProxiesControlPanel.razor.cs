using Blazor.Shared.Network.ControlPanels.Proxies.Models;
using Blazor.Shared.Network.ControlPanels.Proxies.Services;
using ViciOne.Ui.MonochromeIcons.Core.Enums;
using ViciOne.Ui.MonochromeIcons.Core.Extensions;

namespace Blazor.Shared.Network.ControlPanels.Proxies.Components;

public sealed partial class ProxiesControlPanel : NetworkControlPanelBase<ProxiesControlPanelState>
{
    private readonly string _cluster1IconCssClasses = MonochromeIconName.Cluster1.GetCssClasses().ToSpaceSeparated();

    private void AddDoNotProxyDetail()
        => State.DoNotProxyDetails.Add(new DoNotProxyDetail());

    private void RemoveDoNotProxyDetail()
        => State.DoNotProxyDetails.RemoveAt(State.DoNotProxyDetails.Count - 1);
}
