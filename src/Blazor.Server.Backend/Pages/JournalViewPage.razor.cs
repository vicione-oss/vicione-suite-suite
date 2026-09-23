using Microsoft.AspNetCore.Components;
using Sdk.Client.Services;
using Sdk.Journal;
using ViciOne.Ui.Localization.Resources;

namespace Blazor.Server.Backend.Pages;

public partial class JournalViewPage
{
    [Inject] private ILayoutService Layout { get; set; } = default!;
    [Inject] private IJournalMonitoring JournalMonitoring { get; set; } = default!;

    /// <summary>
    /// Filter string in systemd journalctl form: Field=Value entries separated by comma (AND) or plus (OR),
    /// e.g. Field1=Value,Field2=Value+Field3=Value. Matches on different fields must all apply; matches on
    /// the same field accept any of the given values.
    /// <see href="https://github.com/systemd/systemd/blob/1f5d8a6132f12574f524cef72dbda0f7408c4217/man/sd_journal_add_match.xml#L76"/>
    /// </summary>
    [SupplyParameterFromQuery]
    public string Filter { get; set; } = string.Empty;

    [SupplyParameterFromQuery]
    public bool Follow { get; set; }

    protected override void OnInitialized()
    {
        Layout.TitleBarAppName = TechnicalTerms.Journal;

        EnsureFilter();
        base.OnInitialized();
    }

    protected override void OnParametersSet()
    {
        EnsureFilter();
        base.OnParametersSet();
    }

    private void EnsureFilter()
    {
        if (string.IsNullOrEmpty(Filter))
            Filter = string.Join('+', JournalMonitoring.FilterEntries.Select(e => e.Value.Filter));
    }
}
