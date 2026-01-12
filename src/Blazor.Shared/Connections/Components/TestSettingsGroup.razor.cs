using Blazor.Shared.Connections.Contracts;
using Blazor.Shared.Connections.Services;
using Core.Shared.Connections.Contracts;
using Microsoft.AspNetCore.Components;
using ViciOne.Ui.MonochromeIcons.Core.Enums;
using ViciOne.Ui.MonochromeIcons.Core.Extensions;

namespace Blazor.Shared.Connections.Components;

public sealed partial class TestSettingsGroup : ComponentBase, IDisposable
{
    private readonly string _refreshIconCssClasses = MonochromeIconName.Refresh.GetCssClasses(MonochromeIconSize.Small).ToSpaceSeparated();
    private readonly CancellationTokenSource _cancellationTokenSource = new();

    private bool _testConnectionRunning;
    private DateTime _testStartedTimestamp;

    [Parameter]
    public required EditConnectionModel Model { get; set; }

    [Inject] private TestConnectionService TestConnectionService { get; set; } = default!;

    protected override void OnInitialized()
    {
        TestConnectionService.TestStarted += TestServiceOnTestStarted;
        TestConnectionService.TestResultReceived += TestServiceOnTestResultReceived;
    }

    public void Dispose()
    {
        TestConnectionService.TestStarted -= TestServiceOnTestStarted;
        TestConnectionService.TestResultReceived -= TestServiceOnTestResultReceived;

        _cancellationTokenSource.Cancel();
        _cancellationTokenSource.Dispose();
    }

    private async Task TestConnectionButtonClick()
        => await TestConnectionService.TestConnection(Model.Connection);

    private async Task TestServiceOnTestStarted(Guid connectionId)
    {
        if (Equals(connectionId, Model.Id))
        {
            _testStartedTimestamp = DateTime.UtcNow;
            _testConnectionRunning = true;

            await InvokeAsync(StateHasChanged);
        }
    }

    private async Task TestServiceOnTestResultReceived(TestConnectionResult result)
    {
        if (Equals(result.ConnectionId, Model.Id))
        {
            var testDuration = DateTime.UtcNow - _testStartedTimestamp;

            var totalSecondsElapsed = testDuration.TotalSeconds;
            if (totalSecondsElapsed < 1)
            {
                var delay = 1000 - (int)Math.Floor(1000 * totalSecondsElapsed);

                await Task.Delay(delay, _cancellationTokenSource.Token); // allow browser to render loading indication
            }

            Model.TestResult = result;
            _testConnectionRunning = false;

            await InvokeAsync(StateHasChanged);
        }
    }
}

