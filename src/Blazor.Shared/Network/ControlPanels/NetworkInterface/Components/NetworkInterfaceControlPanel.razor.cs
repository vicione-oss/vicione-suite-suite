using Blazor.Shared.Network.ControlPanels.NetworkInterface.Components.Localization.Extensions;
using Blazor.Shared.Network.ControlPanels.NetworkInterface.Models;
using Blazor.Shared.Network.ControlPanels.NetworkInterface.Services;
using Blazor.Shared.Settings.NetworkInterface.Enums;
using Core.Shared.HostManagement.Requests;
using Core.Shared.HostManagement.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Sdk.Client.Infrastructure;
using Sdk.NetworkStatus.Requests;
using ViciOne.Ui.Localization.Resources;
using ViciOne.Ui.MonochromeIcons.Core.Enums;
using ViciOne.Ui.MonochromeIcons.Core.Extensions;

namespace Blazor.Shared.Network.ControlPanels.NetworkInterface.Components;

public sealed partial class NetworkInterfaceControlPanel : NetworkControlPanelBase<NetworkInterfaceControlPanelState>
{
    private readonly CancellationTokenSource _cancellationTokenSource = new();

    private readonly string _cluster1IconCssClasses = MonochromeIconName.Cluster1.GetCssClasses().ToSpaceSeparated();
    private readonly string _refreshIconCssClasses = MonochromeIconName.Refresh.GetCssClasses(MonochromeIconSize.Small).ToSpaceSeparated();

    private sealed record TestConnectionResult
    {
        public required bool IsDefaultRouteConfigured { get; init; }
        public required bool IsDefaultGatewayAvailable { get; init; }
        public required bool IsInternetAvailable { get; init; }
        public required bool IsDnsFunctional { get; init; }
        public required string State { get; init; }
    }

    private bool _testConnectionRunning;
    private TestConnectionResult? _testConnectionResult;
    private bool _testConnectionExecutedAtLeastOnce;
    private bool _showErrorDialog;
    private string? _errorMessage;
    private DateTimeOffset? _dhcpLeaseStart;
    private DateTimeOffset? _dhcpLeaseEnd;
    private TimeSpan? _dhcpLeaseDuration;

    [Inject]
    private ILogger<NetworkInterfaceControlPanel> Logger { get; set; } = default!;

    [Inject]
    private IUiMediator UiMediator { get; set; } = default!;

    [Inject(Key = Sdk.Constants.ClientTimeProviderServiceKey)]
    private TimeProvider TimeProvider { get; set; } = default!;

    [Inject]
    private ISystemConfigurationService SystemConfigurationService { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        try
        {
            await SystemConfigurationService.Initialize(_cancellationTokenSource.Token);

            SystemConfigurationService.SystemConfigurationChanged += OnSystemConfigurationChanged;

            await SetDhcpLeaseInformation();
        }
        catch (OperationCanceledException)
        {
            // Nothing to do here, return gracefully
            return;
        }
        catch (ObjectDisposedException)
        {
            // Semaphore or CancellationTokenSource already disposed, nothing we can do, return gracefully
            return;
        }
    }

    protected override async ValueTask DisposeAsyncCore()
    {
        await _cancellationTokenSource.CancelAsync();
        _cancellationTokenSource.Dispose();

        SystemConfigurationService.SystemConfigurationChanged -= OnSystemConfigurationChanged;

        await base.DisposeAsyncCore();
    }

    private string GetPhysicalAddress()
    {
        var networkInterface = SystemConfigurationService.SystemConfiguration.NetworkInterfacesSettings.NetworkInterfaces
            .ElementAtOrDefault(State.NetworkInterfaceIndex);

        if (networkInterface is not null)
            return networkInterface.CommonInformation.PhysicalAddress;
        else
            return CommonVocabulary.Unknown;
    }

    private void AddAdditionalIPv4Detail()
        => State.AdditionalIpV4Details.Add(new NetworkInterfaceIPv4Detail());

    private void RemoveAdditionalIPv4Detail(NetworkInterfaceIPv4Detail ipV4Detail)
        => State.AdditionalIpV4Details.Remove(ipV4Detail);

    private async Task TestConnectionButtonClick()
    {
        _testConnectionRunning = true;

        try
        {
            var request = new GetNetworkStatusInformation(State.Name);
            var response = await UiMediator.Request<GetNetworkStatusInformation, GetNetworkStatusInformationResponse>(request, _cancellationTokenSource.Token);

            if (response.NetworkStatusInformation is not null)
            {
                _testConnectionResult = new TestConnectionResult
                {
                    IsDefaultGatewayAvailable = response.NetworkStatusInformation.IsDefaultGatewayAvailable,
                    IsDefaultRouteConfigured = response.NetworkStatusInformation.IsDefaultRouteConfigured,
                    IsInternetAvailable = response.NetworkStatusInformation.IsInternetAvailable,
                    IsDnsFunctional = response.NetworkStatusInformation.IsDNSFunctional,
                    State = response.NetworkStatusInformation.InterfaceState
                };
            }
            else
            {
                _testConnectionResult = null;
            }
        }
        catch (Exception exception)
        {
            GetNetworkStatusInformationRequestFailed(Logger, exception, nameof(UiMediator.Request));
        }
        finally
        {
            await RenderLoadingIndication();

            _testConnectionRunning = false;
            _testConnectionExecutedAtLeastOnce = true;
        }
    }

    [LoggerMessage(1, LogLevel.Error, "{Call} failed)")]
    private static partial void GetNetworkStatusInformationRequestFailed(ILogger<NetworkInterfaceControlPanel> logger,
        Exception exception, string call);

    private async Task IpV4ConfigurationModeChanged()
    {
        if (State.IpV4ConfigurationMode == IpConfigurationMode.AutomaticDhcp)
        {
            await SetDhcpLeaseInformation();

            State.DhcpLeaseSettingsGroupExpanded = true;
        }
    }

    private async Task InterfaceEnabledChanged()
    {
        if (State.Enabled)
            _testConnectionExecutedAtLeastOnce = false;

        await BeginEdit();
    }

    private async Task RenewDhcpLease()
    {
        State.DhcpLeaseFetching = true;

        var response = await UiMediator.Request<RenewDHCPLease, RenewDHCPLeaseResponse>(new RenewDHCPLease(State.Name), _cancellationTokenSource.Token);

        if (response.RequestError is not null)
        {
            _errorMessage = response.RequestError.ErrorCode switch
            {
                1 or 3 => response.RequestError.Message,
                _ => null,
            };

            _showErrorDialog = true;
        }

        if (response.RequestError is not null || response.DHCPLease is null)
        {
            await RenderLoadingIndication();
            State.DhcpLeaseFetching = false;
        }
    }

    public async Task OnSystemConfigurationChanged()
    {
        await SetDhcpLeaseInformation();
        await InvokeAsync(StateHasChanged);
    }

    private async Task SetDhcpLeaseInformation()
    {
        var networkInterfaceDetail = SystemConfigurationService.SystemConfiguration.NetworkInterfacesSettings.NetworkInterfaces
                .ElementAtOrDefault(State.NetworkInterfaceIndex);

        var dhcpLease = networkInterfaceDetail?.IPv4?.DHCPLease;

        _dhcpLeaseStart = dhcpLease?.LeaseObtained.AdjustToTimeZone(TimeProvider.LocalTimeZone);
        _dhcpLeaseEnd = dhcpLease?.LeaseExpires.AdjustToTimeZone(TimeProvider.LocalTimeZone);

        if (_dhcpLeaseStart.HasValue && _dhcpLeaseEnd.HasValue)
            _dhcpLeaseDuration = _dhcpLeaseEnd.Value - _dhcpLeaseStart.Value;
        else
            _dhcpLeaseDuration = null;

        await RenderLoadingIndication();
        State.DhcpLeaseFetching = false;
    }

    private async Task RenderLoadingIndication()
        => await Task.Delay(1000, _cancellationTokenSource.Token); // allow browser to render loading indication
}
