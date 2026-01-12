using System.Collections.ObjectModel;
using Blazor.Shared.Logging.Contracts;
using Microsoft.AspNetCore.Components;

namespace Blazor.Shared.Logging;

public sealed partial class JournalListView : ComponentBase, IDisposable
{
    [Inject(Key = Sdk.Constants.ClientTimeProviderServiceKey)] private TimeProvider TimeProvider { get; set; } = default!;

    [Parameter] public ObservableCollection<JournalEntry> Entries { get; set; } = [];
    [Parameter] public Action? LoadMore { get; set; }
    [Parameter] public string ErrorMessage { get; set; } = string.Empty;

    protected override async Task OnInitializedAsync()
    {
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

    public void Dispose()
    {
        Entries.CollectionChanged -= CollectionChanged;
        Entries.Clear();
        LoadMore = null;
    }
}
