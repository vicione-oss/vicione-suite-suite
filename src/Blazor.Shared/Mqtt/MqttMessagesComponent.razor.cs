using Blazor.Shared.Mqtt.Contracts;
using Blazor.Shared.Mqtt.Localization;
using Blazor.Shared.Mqtt.Services;
using Microsoft.AspNetCore.Components;

namespace Blazor.Shared.Mqtt;

public sealed partial class MqttMessagesComponent : IDisposable
{
    [Inject]
    private MqttViewerComponentService ViewerService { get; set; } = default!;

    protected override Task OnInitializedAsync()
    {
        ViewerService.PageRefreshRequested += OnPageRefreshRequested;
        return Task.CompletedTask;
    }

    private string GetHeadline()
    {
        return ViewerService.CurrentFilter switch
        {
            MqttFilterTypes.TopicNode => MqttViewer.SelectedMessages,
            _ => MqttViewer.AllMessages,
        };
    }

    private async Task OnPageRefreshRequested()
        => await InvokeAsync(StateHasChanged);

    public void Dispose()
        => ViewerService.PageRefreshRequested -= OnPageRefreshRequested;
}
