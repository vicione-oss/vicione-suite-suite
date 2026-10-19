using Blazor.Shared.Network.ControlPanels.NetworkInterface.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Sdk.Client.Infrastructure;
using Sdk.NetworkStatus.Requests;
using ViciOne.Ui.MonochromeIcons.Core.Enums;
using ViciOne.Ui.MonochromeIcons.Core.Extensions;

namespace Blazor.Shared.Network.ControlPanels.NetworkInterface.Components;

public sealed partial class NetworkInterfaceGeneralControlPanel : NetworkControlPanelBase<NetworkInterfaceControlPanelState>
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

    [Inject]
    private ILogger<NetworkInterfaceGeneralControlPanel> Logger { get; set; } = default!;

    [Inject]
    private IUiMediator UiMediator { get; set; } = default!;

    protected override async ValueTask DisposeAsyncCore()
    {
        await _cancellationTokenSource.CancelAsync();
        _cancellationTokenSource.Dispose();

        await base.DisposeAsyncCore();
    }

    protected override async Task BeginEdit()
    {
        await base.BeginEdit();

        State.GeneralHasChanges = true;
    }

    protected override async Task CancelEdit()
    {
        await base.CancelEdit();

        State.GeneralHasChanges = false;
    }

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
            LogGetNetworkStatusInformationRequestFailed(Logger, exception, nameof(UiMediator.Request));
        }
        finally
        {
            await RenderLoadingIndication();

            _testConnectionRunning = false;
            _testConnectionExecutedAtLeastOnce = true;
        }
    }

    private async Task InterfaceEnabledChanged()
    {
        if (State.Enabled)
            _testConnectionExecutedAtLeastOnce = false;

        await BeginEdit();
    }

    // Live readouts of system configuration that the TcpIp/Hardware tabs edit (VLan info, MAC address), so this
    // tab needs to re-render when it changes too, even though it does not itself own that state.
    protected override Task SystemConfigurationChanged(CancellationToken cancellationToken)
    {
        StateHasChanged();

        return Task.CompletedTask;
    }

    private async Task RenderLoadingIndication()
        => await Task.Delay(1000, _cancellationTokenSource.Token); // allow browser to render loading indication

    [LoggerMessage(LogLevel.Error, "{Call} failed")]
    private static partial void LogGetNetworkStatusInformationRequestFailed(ILogger<NetworkInterfaceGeneralControlPanel> logger, Exception exception, string call);
}
