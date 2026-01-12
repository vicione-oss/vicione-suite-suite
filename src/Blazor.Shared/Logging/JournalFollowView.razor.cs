using System.Collections.ObjectModel;
using System.Runtime.Versioning;
using Blazor.Shared.Logging.Contracts;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using ViciOne.Journal;

namespace Blazor.Shared.Logging;

[SupportedOSPlatform("linux")]
public sealed partial class JournalFollowView : ComponentBase, IDisposable
{
    private JournalFollowSession? _follower;

    [Inject] private JournalService JournalService { get; set; } = default!;
    [Inject] private ILogger<JournalView> Logger { get; set; } = default!;
    [Parameter] public string Filter { get; set; } = string.Empty;

    private ObservableCollection<JournalEntry>? Entries { get; set; }
    private string? ErrorMessage { get; set; }

    protected override async Task OnInitializedAsync()
    {
        try
        {
            using CancellationTokenSource cts = new(3_000);
            _follower = await JournalService.CreateFollower(TimeSpan.FromMilliseconds(1000), cts.Token);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }

        if (_follower is not null)
        {
            try
            {
                _follower.SetFilter(Filter);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to set filter '{Filter}'.", Filter);

                _follower.Dispose();
                _follower = null;
                ErrorMessage = ex.Message;
            }
        }

        Entries = [];

        if (_follower is not null)
        {
            _follower.NewEntry += LoadEntry;
            _follower.Start(10);
        }

        await base.OnInitializedAsync();
    }

    private void LoadEntry(IReadOnlyDictionary<string, string> entry)
    {
        if (Entries is null || _follower is null)
            return;

        if (JournalEntryParser.TryParseEntry(entry, out var parsedEntry))
        {
            Entries.Add(parsedEntry);
            InvokeAsync(StateHasChanged);
        }
    }

    public void Dispose()
    {
        if (_follower is not null)
        {
            _follower.NewEntry -= LoadEntry;
            _follower.Stop().GetAwaiter().GetResult();
            _follower.Dispose();
            _follower = null;
        }
    }
}
