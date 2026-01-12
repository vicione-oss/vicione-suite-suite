using Blazor.Shared.Network.ControlPanels.RemoteAccess.Services;
using ViciOne.Ui.MonochromeIcons.Core.Enums;
using ViciOne.Ui.MonochromeIcons.Core.Extensions;

namespace Blazor.Shared.Network.ControlPanels.RemoteAccess.Components;

public sealed partial class RemoteAccessControlPanel : NetworkControlPanelBase<RemoteAccessControlPanelState>
{
    private readonly string _cluster1IconCssClasses = MonochromeIconName.Cluster1.GetCssClasses().ToSpaceSeparated();

    private async Task IsSecureShellChanged()
    {
        if (State.Terminal.IsSecureShell == State.IsSecureShellInitial)
        {
            await CancelEdit();
            return;
        }

        await BeginEdit();
    }

    private async Task IsMoneoRcChanged()
    {
        if (State.Terminal.IsMoneoRc == State.IsMoneoRcInitial)
        {
            await CancelEdit();
            return;
        }

        await BeginEdit();
    }
}
