using Blazor.Shared.Network.ControlPanels.NetworkInterface.Extensions;
using Blazor.Shared.Network.ControlPanels.NetworkInterface.Models;
using Blazor.Shared.Network.ControlPanels.NetworkInterface.Services;
using Blazor.Shared.Network.Extensions;
using Blazor.Shared.Settings.NetworkInterface.Enums;
using Core.Shared.HostManagement.Requests;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Sdk.Client.Infrastructure;
using ViciOne.Ui.Localization.Extensions;
using ViciOne.Ui.MonochromeIcons.Core.Enums;
using ViciOne.Ui.MonochromeIcons.Core.Extensions;

namespace Blazor.Shared.Network.ControlPanels.NetworkInterface.Components;

public sealed partial class NetworkInterfaceTcpIpControlPanel : NetworkControlPanelBase<NetworkInterfaceControlPanelState>
{
    private readonly CancellationTokenSource _cancellationTokenSource = new();

    private readonly string _cluster1IconCssClasses = MonochromeIconName.Cluster1.GetCssClasses().ToSpaceSeparated();
    private readonly string _refreshIconCssClasses = MonochromeIconName.Refresh.GetCssClasses(MonochromeIconSize.Small).ToSpaceSeparated();

    private bool _renewingDhcpLease;
    private bool _showErrorDialog;
    private string? _errorMessage;
    private bool _switchToManualConfigurationWarningDialog;
    private IpConfigurationMode _ipV4ConfigurationModeBefore;
    private DateTimeOffset? _dhcpLeaseStart;
    private DateTimeOffset? _dhcpLeaseEnd;
    private TimeSpan? _dhcpLeaseDuration;

    [Inject]
    private ILogger<NetworkInterfaceTcpIpControlPanel> Logger { get; set; } = default!;

    [Inject]
    private IUiMediator UiMediator { get; set; } = default!;

    [Inject(Key = Sdk.Constants.ClientTimeProviderServiceKey)]
    private TimeProvider TimeProvider { get; set; } = default!;

    protected override void OnInitialized()
    {
        base.OnInitialized();

        SetDhcpLeaseInformation();

        State.DhcpLeaseFetching = false;
    }

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        await EnsureDHCPLease();

        SetDhcpLeaseInformation();
    }

    protected override async ValueTask DisposeAsyncCore()
    {
        await _cancellationTokenSource.CancelAsync();
        _cancellationTokenSource.Dispose();

        await base.DisposeAsyncCore();
    }

    protected override async Task BeginEdit()
    {
        await base.BeginEdit();

        State.TcpIpHasChanges = true;
    }

    protected override async Task CancelEdit()
    {
        await base.CancelEdit();

        State.TcpIpHasChanges = false;
    }

    private void AddAdditionalIPv4Detail()
        => State.AdditionalIpV4Details.Add(new NetworkInterfaceIPv4Detail());

    private void RemoveAdditionalIPv4Detail(NetworkInterfaceIPv4Detail ipV4Detail)
        => State.AdditionalIpV4Details.Remove(ipV4Detail);

    private void IpV4ConfigurationModeChanged()
    {
        if (State.IpV4ConfigurationMode is IpConfigurationMode.Manual &&
            SystemConfigurationService.GetStaticDnsNameServers().Count == 0)
        {
            _switchToManualConfigurationWarningDialog = true;
            return;
        }

        _ipV4ConfigurationModeBefore = State.IpV4ConfigurationMode;

        if (State.IpV4ConfigurationMode == IpConfigurationMode.AutomaticDhcp)
        {
            SetDhcpLeaseInformation();

            State.DhcpLeaseFetching = false;
            State.DhcpLeaseSettingsGroupExpanded = true;
        }
    }

    private void OnManualConfigConfirm()
        => _ipV4ConfigurationModeBefore = State.IpV4ConfigurationMode;

    private void OnManualConfigCancel()
        => State.IpV4ConfigurationMode = _ipV4ConfigurationModeBefore;

    private bool CanRenewDhcpLease() => !State.HasUnsavedChanges;

    private async Task RenewDhcpLease()
    {
        _renewingDhcpLease = true;
        State.DhcpLeaseFetching = true;

        try
        {
            var response = await UiMediator.Request<RenewDHCPLease, RenewDHCPLeaseResponse>(new RenewDHCPLease(State.Name), _cancellationTokenSource.Token);

            if (response.RequestError is not null)
            {
                _errorMessage = response.RequestError.ErrorCode switch
                {
                    1 or 3 => response.RequestError.Message,
                    _ => null,
                };

                _showErrorDialog = true;
                return;
            }

            State.DHCPLease = response.DHCPLease;

            SetDhcpLeaseInformation();
        }
        finally
        {
            await RenderLoadingIndication();

            State.DhcpLeaseFetching = false;
            _renewingDhcpLease = false;
        }
    }

    protected override async Task SystemConfigurationChanged(CancellationToken cancellationToken)
    {
        await EnsureDHCPLease();

        SetDhcpLeaseInformation();

        await InvokeAsync(StateHasChanged);
    }

    private async Task EnsureDHCPLease()
    {
        try
        {
            await State.EnsureDHCPLease(UiMediator, SystemConfigurationService, _cancellationTokenSource.Token);
        }
        catch (Exception exception) when (exception is OperationCanceledException or ObjectDisposedException)
        {
            // The tab was closed while the lease was being fetched.
        }
        catch (Exception exception)
        {
            // The lease stays as it was, the DHCP lease group then shows no lease.
            LogGetDHCPLeaseInformationRequestFailed(Logger, exception);
        }
    }

    private void SetDhcpLeaseInformation()
    {
        _dhcpLeaseStart = State.DHCPLease?.LeaseObtained.AdjustToTimeZone(TimeProvider.LocalTimeZone);
        _dhcpLeaseEnd = State.DHCPLease?.LeaseExpires.AdjustToTimeZone(TimeProvider.LocalTimeZone);

        if (_dhcpLeaseStart.HasValue && _dhcpLeaseEnd.HasValue)
            _dhcpLeaseDuration = _dhcpLeaseEnd.Value - _dhcpLeaseStart.Value;
        else
            _dhcpLeaseDuration = null;
    }

    private async Task RenderLoadingIndication()
        => await Task.Delay(1000, _cancellationTokenSource.Token); // allow browser to render loading indication

    [LoggerMessage(LogLevel.Warning, "Fetching the DHCP lease failed")]
    private static partial void LogGetDHCPLeaseInformationRequestFailed(ILogger<NetworkInterfaceTcpIpControlPanel> logger, Exception exception);
}
