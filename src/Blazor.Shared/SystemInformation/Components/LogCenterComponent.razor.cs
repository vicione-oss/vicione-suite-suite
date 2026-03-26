using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using Blazor.Shared.SystemInformation.Colors;
using Blazor.Shared.SystemInformation.Enums;
using Blazor.Shared.SystemInformation.Models;
using Blazor.Shared.SystemInformation.Services;
using Microsoft.AspNetCore.Components;
using Sdk.Journal;
using ViciOne.Ui.MonochromeIcons.Core.Enums;

namespace Blazor.Shared.SystemInformation.Components;

public sealed partial class LogCenterComponent : IDisposable
{
    [Inject] private IJournalMonitoring JournalMonitoring { get; set; } = default!;
    [Inject] private LogCenterService LogCenterService { get; set; } = default!;
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;

    private void OpenJournal()
        => NavigationManager.NavigateTo(Constants.JournalViewRoute);

    private void OpenJournal(JournalFilterEntry filterEntry)
        => NavigationManager.NavigateTo($"{Constants.JournalViewRoute}?filter={UrlEncoder.Default.Encode(filterEntry.Filter)}");

    private IEnumerable<(string Key, LogCenterEntry LogEntry, JournalFilterEntry FilterEntry)> GetEntries()
    {
        return Get().OrderBy(e => e.FilterEntry.Index).Take(3);

        IEnumerable<(string Key, LogCenterEntry LogEntry, JournalFilterEntry FilterEntry)> Get()
        {
            foreach (var entry in LogCenterService.LogCenterEntries)
            {
                if (JournalMonitoring.FilterEntries.TryGetValue(entry.Key, out var filterEntry))
                    yield return (entry.Key, entry.Value, filterEntry);
            }
        }
    }

    private string GetMinutesLabel()
    {
        StringBuilder sb = new();
        sb.Append(Localization.LogCenterComponent.Last);
        sb.Append(' ');

        var minutes = LogCenterService.MinutesCount;

        if (minutes > 1)
            sb.Append(string.Format(CultureInfo.InvariantCulture, ViciOne.Ui.Localization.Resources.CommonPatterns.MultipleMinutes, minutes));
        else
            sb.Append(string.Format(CultureInfo.InvariantCulture, ViciOne.Ui.Localization.Resources.CommonPatterns.SingleMinute, 1));

        return sb.ToString();
    }

    protected override void OnInitialized()
    {
        LogCenterService.Changed += OnChanged;
        base.OnInitialized();
    }

    private void OnChanged()
        => InvokeAsync(StateHasChanged);

    private static string CombineText(LogCenterEntry entry)
        => $"{entry.InfoCount + entry.WarningCount + entry.ErrorCount} {Localization.LogCenterComponent.Items} {entry.WarningCount + entry.ErrorCount} {Localization.LogCenterComponent.Issues}";

    private static LogCenterHtmlColor CalculateColorFromThreshold(LogCenterEntry entry)
        => (entry.WarningCount + entry.ErrorCount) switch
        {
            0 => LogCenterHtmlColor.From(LogCenterColor.Grey),
            < 10 => LogCenterHtmlColor.From(LogCenterColor.Yellow),
            _ => LogCenterHtmlColor.From(LogCenterColor.Red)
        };

    private static MonochromeIconName CalculateIconFromThreshold(LogCenterEntry entry)
        => entry switch
        {
            { ErrorCount: > 0 } => MonochromeIconName.ErrorSolid,
            { WarningCount: > 0, ErrorCount: 0 } => MonochromeIconName.WarningSolid,
            _ => MonochromeIconName.InfoOutlined
        };

    public void Dispose()
        => LogCenterService.Changed -= OnChanged;
}
