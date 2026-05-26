using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using Blazor.Shared.Logging.Contracts;
using Blazor.Shared.SystemInformation.Extensions;
using Microsoft.AspNetCore.Components;
using Sdk.Client.Services;
using Sdk.Instance;

namespace Blazor.Shared.Logging;

public sealed partial class JournalListView : ComponentBase, IDisposable
{
    private bool _isRunningOnLinux;

    [Inject(Key = Sdk.Constants.ClientTimeProviderServiceKey)] private TimeProvider TimeProvider { get; set; } = default!;
    [Inject] private IJsInterop JsInterop { get; set; } = default!;
    [Inject] public IInstanceInformationProvider InformationProvider { get; set; } = default!;

    [Parameter] public ObservableCollection<JournalEntry> Entries { get; set; } = [];
    [Parameter] public Action? LoadMore { get; set; }
    [Parameter] public string ErrorMessage { get; set; } = string.Empty;

    protected override async Task OnInitializedAsync()
    {
        _isRunningOnLinux = OperatingSystem.IsLinux();

        Entries.CollectionChanged -= CollectionChanged;
        Entries.CollectionChanged += CollectionChanged;
        await base.OnInitializedAsync();
    }

    private void CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        => InvokeAsync(StateHasChanged);

    private string ComposeHeader(JournalEntry entry)
    {
        var timestamp = TimeZoneInfo.ConvertTime(entry.Timestamp, TimeProvider.LocalTimeZone);

        if (entry.PID is null)
            return $"{timestamp:MMM dd HH:mm:ss} {entry.Hostname} {entry.Unit}: ";

        return $"{timestamp:MMM dd HH:mm:ss} {entry.Hostname} {entry.Unit}[{entry.PID}]: ";
    }

    /// <summary>
    /// https://wiki.archlinux.org/title/Systemd/Journal
    /// </summary>
    /// <param name="priority"></param>
    /// <returns></returns>
    private static string GetClassFromPriority(int priority)
        => priority switch
        {
            0 => "priority-emergency",
            1 => "priority-alert",
            2 => "priority-critical",
            3 => "priority-error",
            4 => "priority-warning",
            5 => "priority-notice",
            6 => "priority-information",
            7 => "priority-debug",
            _ => "priority-information"
        };

    private async Task DownloadEntriesAsync()
    {
        if (Entries.Count == 0)
            return;

        var content = await FormatAsTxt();
        var fileName = $"{InformationProvider.Local.SerialNumber}-{DateTime.UtcNow:yyyyMMdd-HHmmss}.txt";

        await JsInterop.DownloadAs(content, fileName);
    }

    private async Task<string> FormatAsTxt()
    {
        var sb = new StringBuilder();

        sb.AppendLine(await InformationProvider.GetSystemInformationMarkdown());

        foreach (var entry in Entries)
        {
            var timestamp = TimeZoneInfo.ConvertTime(entry.Timestamp, TimeProvider.LocalTimeZone);
            sb.Append(CultureInfo.InvariantCulture, $"{timestamp:yyyy-MM-dd HH:mm:ss} ");
            sb.Append(CultureInfo.InvariantCulture, $"{entry.Hostname} {entry.Unit}");

            if (entry.PID is not null)
                sb.Append(CultureInfo.InvariantCulture, $"[{entry.PID}]");

            sb.AppendLine(CultureInfo.InvariantCulture, $": {entry.Message}");
        }

        return sb.ToString();
    }

    public void Dispose()
    {
        Entries.CollectionChanged -= CollectionChanged;
        Entries.Clear();
        LoadMore = null;
    }
}
