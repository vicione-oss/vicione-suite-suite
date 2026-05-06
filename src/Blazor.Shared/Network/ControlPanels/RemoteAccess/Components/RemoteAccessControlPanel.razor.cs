using Blazor.Shared.Network.ControlPanels.RemoteAccess.Extensions;
using Blazor.Shared.Network.ControlPanels.RemoteAccess.Services;
using Core.Shared.HostManagement;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using ViciOne.Ui.MonochromeIcons.Core.Enums;
using ViciOne.Ui.MonochromeIcons.Core.Extensions;

namespace Blazor.Shared.Network.ControlPanels.RemoteAccess.Components;

public sealed partial class RemoteAccessControlPanel : NetworkControlPanelBase<RemoteAccessControlPanelState>
{
    private readonly string _cluster1IconCssClasses = MonochromeIconName.Cluster1.GetCssClasses().ToSpaceSeparated();

    [Inject]
    private IConfiguration Configuration { get; set; } = default!;

    [Inject]
    private IOptions<HostManagementOptions> HostManagementOptions { get; set; } = default!;

    protected override Task SystemConfigurationChanged(CancellationToken cancellationToken)
    {
        State.Initialize(SystemConfigurationService, Configuration, HostManagementOptions);
        return InvokeAsync(StateHasChanged);
    }

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
