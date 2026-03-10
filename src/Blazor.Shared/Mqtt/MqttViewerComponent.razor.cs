using System.Diagnostics;
using Blazor.Shared.Mqtt.Localization;
using Blazor.Shared.Mqtt.Services;
using Microsoft.AspNetCore.Components;
using Sdk.Client.Infrastructure;
using Sdk.Client.Services;
using Sdk.Connections.Contracts;
using Sdk.Connections.Requests;
using ViciOne.Ui.Localization.Resources;

namespace Blazor.Shared.Mqtt;

public sealed partial class MqttViewerComponent : IAsyncDisposable
{
    internal const int MinReloadInterval = 200;
    internal const int MaxReloadInterval = 5000;
    internal const int DefaultReloadInterval = 1000;

    private string _connectionStatusIcon = "mdi mdi-wifi";
    private string _connectionStatusText = MqttViewer.ConnectTitle;
    private List<Connection> _connections = [];
    private readonly int[] _reloadIntervals =
    [
        MaxReloadInterval,
        2000,
        DefaultReloadInterval,
        500,
        MinReloadInterval,
    ];
    private Connection? _selectedConnection;

    private bool _isConnecting;
    private bool _isConnected;
    private int _selectedReloadInterval;
    private readonly Stopwatch _stopwatch = new();
    private System.Timers.Timer _timer = new(DefaultReloadInterval);

    [Inject] public IUiMediator Mediator { get; set; } = default!;
    [Inject] public IMqttService MqttService { get; set; } = default!;
    [Inject] public MqttViewerComponentService ViewerService { get; set; } = default!;
    [Inject] private ILayoutService LayoutService { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        _selectedReloadInterval = ViewerService.LastReloadInterval ?? DefaultReloadInterval;

        await LoadConnections();

        ViewerService.MqttConnected += OnMqttConnected;
        ViewerService.MqttDisconnected += OnMqttDisconnected;
        ViewerService.PageRefreshRequested += OnPageRefreshRequested;

        _timer.Elapsed += OnTimerElapsed;

        if (_selectedReloadInterval != DefaultReloadInterval)
            _timer.Interval = _selectedReloadInterval;

        LayoutService.TitleBarAppName = MqttViewer.Title;
    }

    private async Task LoadConnections()
    {
        var request = new GetConnections(null, [ConnectionType.Mqtt.Name]);
        _connections = (await Mediator.Request<GetConnections, GetConnectionsResponse>(request, CancellationToken.None)).Connections;

        if (_connections.Count != 0)
            _selectedConnection = _connections.First();
        else
            ViewerService.ErrorMessage = MqttViewer.NoConnections;
    }

    private async Task ToggleConnection()
    {
        try
        {
            if (!ViewerService.IsConnected)
            {
                if (_selectedConnection is null || _isConnecting)
                    return;

                _isConnecting = true;
                await InvokeAsync(StateHasChanged);

                await ViewerService.ConnectClient(_selectedConnection);
            }
            else
            {
                await ViewerService.DisconnectClient();
            }
        }
        catch (Exception e)
        {
            ViewerService.ErrorMessage = e.Message;
        }
        finally
        {
            _isConnecting = false;
            await InvokeAsync(StateHasChanged);
        }
    }

    private Task OnMqttConnected()
    {
        _stopwatch.Start();
        _timer.Start();
        _isConnected = true;

        return SetConnectionIcon(true);
    }

    private void ReloadIntervalChanged(int interval)
    {
        _selectedReloadInterval = interval;
        if (interval is < MinReloadInterval or > MaxReloadInterval)
        {
            ViewerService.ErrorMessage = MqttViewer.IntervalError;
            return;
        }

        _timer.Elapsed -= OnTimerElapsed;
        _timer.Stop();
        _timer.Dispose();
        _timer = new(interval);
        _timer.Elapsed += OnTimerElapsed;
        _timer.Start();
    }

    private async void OnTimerElapsed(object? sender, System.Timers.ElapsedEventArgs e)
        => await ViewerService.RequestRefresh();

    private async Task OnPageRefreshRequested()
        => await InvokeAsync(StateHasChanged);

    private string GetLifetimeInfo()
    {
        var messagesPerSecond = _stopwatch.Elapsed.TotalSeconds > 0
            ? MqttService.ReceivedMessages / _stopwatch.Elapsed.TotalSeconds
            : 0d;

        return $"{MqttViewer.MessageCounter} {MqttService.ReceivedMessages} = {messagesPerSecond:0.00} M/s Time: {_stopwatch.Elapsed.ToString("hh\\:mm\\:ss", null)}";
    }

    private Task OnMqttDisconnected()
    {
        _stopwatch.Stop();
        _stopwatch.Reset();
        _timer.Stop();
        _isConnected = false;

        return SetConnectionIcon(false);
    }

    public async ValueTask DisposeAsync()
    {
        ViewerService.MqttConnected -= OnMqttConnected;
        ViewerService.MqttDisconnected -= OnMqttDisconnected;
        ViewerService.PageRefreshRequested -= OnPageRefreshRequested;
        await ViewerService.DisconnectClient();

        ViewerService.LastReloadInterval = _selectedReloadInterval;

        await MqttService.DisposeAsync();
        _timer.Elapsed -= OnTimerElapsed;
        _timer.Dispose();

        GC.SuppressFinalize(this);
    }

    private Task SetConnectionIcon(bool connected)
    {
        _connectionStatusIcon = connected ? "mdi mdi-wifi-off" : "mdi mdi-wifi";
        _connectionStatusText = connected ? CommonVocabulary.DisconnectVerb : MqttViewer.ConnectTitle;
        return InvokeAsync(StateHasChanged);
    }
}
