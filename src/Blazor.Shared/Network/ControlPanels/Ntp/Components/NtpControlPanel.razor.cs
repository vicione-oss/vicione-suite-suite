using Blazor.Shared.Network.ControlPanels.Ntp.Extensions;
using Blazor.Shared.Network.ControlPanels.Ntp.Models;
using Blazor.Shared.Network.ControlPanels.Ntp.Services;
using Microsoft.AspNetCore.Components;
using Sdk.Client.Infrastructure;
using ViciOne.Ui.MonochromeIcons.Core.Enums;
using ViciOne.Ui.MonochromeIcons.Core.Extensions;

namespace Blazor.Shared.Network.ControlPanels.Ntp.Components;

public sealed partial class NtpControlPanel : NetworkControlPanelBase<NtpControlPanelState>
{
    private readonly string _cluster1IconCssClasses = MonochromeIconName.Cluster1.GetCssClasses().ToSpaceSeparated();

    [Inject]
    private IUiMediator Mediator { get; set; } = default!;

    protected override async Task SystemConfigurationChanged(CancellationToken cancellationToken)
    {
        await State.Initialize(SystemConfigurationService, Mediator, cancellationToken);
        await InvokeAsync(StateHasChanged);
    }

    private void AddNtpServerDetail() => State.NtpServerDetails.Add(new NtpServerDetail());

    private void RemoveNtpServerDetail() => State.NtpServerDetails.RemoveAt(State.NtpServerDetails.Count - 1);

    private void CopyDefaultNtpServersToServerList()
    {
        var fallBackServersNotAlreadyInList = State.FallbackNtpServerDetails.ExceptBy(
            State.NtpServerDetails.Select(detail => detail.IpAddressOrHostname),
            fallback => fallback.IpAddressOrHostname,
            StringComparer.OrdinalIgnoreCase);

        CopyFallbackServersToListOfUsedServersPreventingThemBeingAltered(fallBackServersNotAlreadyInList);
    }

    private void CopyFallbackServersToListOfUsedServersPreventingThemBeingAltered(
        IEnumerable<NtpServerDetail> fallBackServersNotAlreadyInList)
    {
        State.NtpServerDetails.AddRange(fallBackServersNotAlreadyInList
            .Select(fallback => new NtpServerDetail { IpAddressOrHostname = fallback.IpAddressOrHostname }));
    }
}
