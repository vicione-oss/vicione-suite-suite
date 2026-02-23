using System.Collections.ObjectModel;
using System.Runtime.Versioning;
using Blazor.Shared.Logging.Contracts;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using ViciOne.Journal;

namespace Blazor.Shared.Logging;

[SupportedOSPlatform("linux")]
public sealed partial class JournalView : ComponentBase, IDisposable
{
    private JournalLookBehindSession? _lookBehind;

    [Inject] private IJournalService JournalService { get; set; } = default!;
    [Inject] private ILogger<JournalView> Logger { get; set; } = default!;
    [Parameter] public string Filter { get; set; } = string.Empty;

    private ObservableCollection<JournalEntry>? Entries { get; set; }
    private string? ErrorMessage { get; set; }

    protected override async Task OnInitializedAsync()
    {
        await Initialize();
        LoadMoreEntries();
        await base.OnInitializedAsync();
    }

    protected override async Task OnParametersSetAsync()
    {
        await Initialize();
        LoadMoreEntries();
        await base.OnParametersSetAsync();
    }

    private async Task Initialize()
    {
        try
        {
            if (_lookBehind is not null)
            {
                Entries = null;
                _lookBehind.Dispose();
                _lookBehind = null;
            }

            using CancellationTokenSource cts = new(3_000);
            _lookBehind = await JournalService.CreateGeneric(cts.Token).ToLookBehind();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }

        if (_lookBehind is not null)
        {
            try
            {
                _lookBehind.SetFilter(Filter);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to set filter '{Filter}'.", Filter);

                _lookBehind.Dispose();
                _lookBehind = null;
                ErrorMessage = ex.Message;
            }
        }

        Entries = [];
    }

    private void LoadMoreEntries()
    {
        if (Entries is null || _lookBehind is null)
            return;

        foreach (var entry in _lookBehind.GetLogs(20))
        {
            if (JournalEntryParser.TryParseEntry(entry, out var parsedEntry))
                Entries.Insert(0, parsedEntry);
        }
    }

    public void Dispose()
        => _lookBehind?.Dispose();

}
