using Blazor.Shared.Network.ControlPanels.Ntp.Models;
using Blazor.Shared.Network.ControlPanels.Ntp.Services;
using ViciOne.Ui.MonochromeIcons.Core.Enums;
using ViciOne.Ui.MonochromeIcons.Core.Extensions;

namespace Blazor.Shared.Network.ControlPanels.Ntp.Components;

public sealed partial class NtpControlPanel : NetworkControlPanelBase<NtpControlPanelState>
{
    private readonly string _cluster1IconCssClasses = MonochromeIconName.Cluster1.GetCssClasses().ToSpaceSeparated();

    private void AddNtpServerDetail()
        => State.NtpServerDetails.Add(new NtpServerDetail());

    private void RemoveNtpServerDetail()
        => State.NtpServerDetails.RemoveAt(State.NtpServerDetails.Count - 1);
}
