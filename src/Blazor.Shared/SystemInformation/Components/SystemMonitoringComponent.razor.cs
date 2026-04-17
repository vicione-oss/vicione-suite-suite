using Blazor.Shared.SystemInformation.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Sdk.Client.Extensions;
using Sdk.Client.Services;

namespace Blazor.Shared.SystemInformation.Components;

public sealed partial class SystemMonitoringComponent : IAsyncDisposable
{
    private IJSObjectReference? _jsModuleReference;
    private IJSObjectReference? _monitoring;
    private ElementReference _canvasRef;
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    [Inject(Key = Sdk.Constants.ClientTimeProviderServiceKey)]
    private TimeProvider TimeProvider { get; set; } = default!;

    [Inject]
    public MonitoringService MonitoringService { get; set; } = default!;

    [Inject]
    public IJsInterop JsInterop { get; set; } = default!;

    [Inject]
    public ILogger<SystemMonitoringComponent> Logger { get; set; } = default!;

    [Parameter]
    public int TimeSpanHours { get; set; } = 24;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender)
            return;

        await _semaphore.WaitAsync();

        try
        {
            _jsModuleReference = await JsInterop.IncludeModuleScript<SharedClientModule>("system-monitoring-component.js");
            if (_jsModuleReference is not null)
            {
                _monitoring = await _jsModuleReference.InvokeConstructorAsync("Monitoring", _canvasRef, TimeProvider.LocalTimeZone.GetUtcOffset(DateTimeOffset.UtcNow).TotalMinutes, TimeSpanHours);

                if (_monitoring is null)
                    return;

                if (OperatingSystem.IsLinux())
                {
                    await SetMetrics();

                    MonitoringService.GraphValuesChanged += SetMetrics;

                    return;
                }

                await _monitoring.InvokeVoidAsync("generateTestData");
                await _monitoring.InvokeVoidAsync("render");
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private async Task SetMetrics()
    {
        if (_monitoring is null)
            return;

        await _monitoring.InvokeVoidAsync("setValue", MonitoringService.CpuGraphValues, "cpu");
        await _monitoring.InvokeVoidAsync("setValue", MonitoringService.RamGraphValues, "ram");
        await _monitoring.InvokeVoidAsync("setValue", MonitoringService.HddGraphValues, "hdd");
        await _monitoring.InvokeVoidAsync("setValue", MonitoringService.NetGraphValues, "net");
        await _monitoring.InvokeVoidAsync("render");
    }

    public async ValueTask DisposeAsync()
    {
        await _semaphore.WaitAsync();

        try
        {
            MonitoringService.GraphValuesChanged -= SetMetrics;

            await _jsModuleReference.TryDisposeAsync(Logger);
            await _monitoring.TryDisposeAsync(Logger);

            _jsModuleReference = null;
            _monitoring = null;
        }
        finally
        {
            _semaphore.Release();
        }

        _semaphore.Dispose();
    }
}
