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
    /// Filter string is based on systemd journalctl. Field=Value entries separated by comma (logical AND) or plus (logical OR).
    /// <see href="https://github.com/systemd/systemd/blob/1f5d8a6132f12574f524cef72dbda0f7408c4217/man/sd_journal_add_match.xml#L76"/>
    /// If a match is applied, only entries with this field set will be iterated. Multiple matches may be active at the
    /// same time: If they apply to different fields, only entries with both fields set like this will be iterated. If
    /// they apply to the same fields, only entries where the field takes one of the specified values will be iterated.
    ///
    /// e.g. Field1=Value,Field2=Value+Field3=Value
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
