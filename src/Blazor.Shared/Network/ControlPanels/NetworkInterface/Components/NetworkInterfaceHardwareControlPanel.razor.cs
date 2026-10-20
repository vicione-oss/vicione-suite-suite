using Blazor.Shared.Network.ControlPanels.NetworkInterface.Services;
using ViciOne.Ui.MonochromeIcons.Core.Enums;
using ViciOne.Ui.MonochromeIcons.Core.Extensions;

namespace Blazor.Shared.Network.ControlPanels.NetworkInterface.Components;

public sealed partial class NetworkInterfaceHardwareControlPanel : NetworkControlPanelBase<NetworkInterfaceControlPanelState>
{
    private readonly string _cluster1IconCssClasses = MonochromeIconName.Cluster1.GetCssClasses().ToSpaceSeparated();

    protected override async Task BeginEdit()
    {
        await base.BeginEdit();

        State.HardwareHasChanges = true;
    }

    protected override async Task CancelEdit()
    {
        await base.CancelEdit();

        State.HardwareHasChanges = false;
    }

    protected override Task SystemConfigurationChanged(CancellationToken cancellationToken)
        => InvokeAsync(StateHasChanged);

    private async Task MacAddressManuallyChanged()
    {
        if (!State.MacAddressManually)
            State.MacAddress = string.Empty;

        await BeginEdit();
    }
}
