using Blazor.Shared.Logging.Contracts;
using Blazor.Shared.Services;
using DevExpress.Blazor;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Sdk.Client.Services;

namespace Blazor.Shared.Logging;

public sealed partial class LogViewComponent : ComponentBase, IDisposable
{
    [Inject] private IBackendLogService LogService { get; set; } = default!;
    [Inject] private ILayoutService Layout { get; set; } = default!;
    [Inject] private ILogger<LogViewComponent> Logger { get; set; } = default!;
    [Inject] private IJsInterop JsInterop { get; set; } = default!;

    private LogViewModel Model { get; } = new();

    private LogLevel SelectedLogLevel
    {
        get => Model.SelectedLogLevel;
        set
        {
            Model.SelectedLogLevel = value;
            OnLogLevelChanged(value);
        }
    }

    public void Dispose()
    {
        Model.LogText = null;
        Model.SelectedLogName = null;
    }

    protected override async Task OnInitializedAsync()
    {
        Layout.TitleBarAppName = Localization.LogView.LogViewTitle;

        await InitLogLevel();
        await InitLogFiles();
    }

    private async Task InitLogFiles()
    {
        try
        {
            var paths = await LogService.GetLogPaths();

            Model.SetLogPaths(paths);
        }
        catch (Exception e)
        {
            Model.LogText = e.Message;
        }
    }

    private async Task InitLogLevel()
        => Model.SelectedLogLevel = await LogService.GetLogLevel();

    private async Task DisplayLog(string? logName)
    {
        try
        {
            if (string.IsNullOrEmpty(logName))
                return;

            Logger.LogDebug("Display logfile={File}", logName);

            Model.LogText = string.Empty;
            Model.IsLoading = true;

            await using var stream = await LogService.GetLog(logName);
            using var reader = new StreamReader(stream);

            Model.LogText = await reader.ReadToEndAsync();
            Model.SelectedLogName = logName;
        }
        catch (Exception e)
        {
            Model.LogText = e.Message;
        }
        finally { Model.IsLoading = false; }
    }

    private async Task DownloadLogfile(string? logName)
    {
        try
        {
            if (string.IsNullOrEmpty(logName))
                return;

            Logger.LogDebug("Download logfile={File}", logName);

            await using var stream = await LogService.GetLog(logName);
            await JsInterop.DownloadAs(stream, logName);
        }
        catch (Exception e)
        {
            Model.LogText = e.Message;
        }
    }

    private async Task OnItemClick(ToolbarItemClickEventArgs args)
    {
        switch (args.ItemName)
        {
            case nameof(DisplayLog):
                await DisplayLog(Model.SelectedLogName);
                break;

            case nameof(DownloadLogfile):
                await DownloadLogfile(Model.SelectedLogName);
                break;
        }
    }

    private Task OnLogLevelChanged(LogLevel level)
    {
        try
        {
            Logger.LogDebug("LogLevelChanged to {LogLevel}", level);

            return LogService.SetLogLevel(level);
        }
        catch (Exception e)
        {
            Logger.LogError(e, nameof(OnLogLevelChanged));
            Model.LogText = e.Message;
        }

        return Task.CompletedTask;
    }

    private bool CanDisplayLogName()
        => Model.SelectedLogName is not null &&
           !Model.SelectedLogName.EndsWith("gz", StringComparison.OrdinalIgnoreCase);
}
