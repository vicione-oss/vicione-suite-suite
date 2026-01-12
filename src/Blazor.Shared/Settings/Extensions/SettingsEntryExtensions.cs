using System.Diagnostics.CodeAnalysis;
using Blazor.Shared.Settings.Comparers;
using Blazor.Shared.Settings.Models;

namespace Blazor.Shared.Settings.Extensions;

internal static class SettingsEntryExtensions
{
    public static IOrderedEnumerable<SettingsEntry> Sort(this IEnumerable<SettingsEntry> settingsEntries)
        => settingsEntries.OrderBy(settingsEntry => settingsEntry.Position, new OptionalPositionComparer())
            .ThenBy(settingsEntry => settingsEntry.Title);

    public static bool TryGetValue(this IDictionary<SettingsEntriesKey,
        IEnumerable<SettingsEntry>> settingsEntriesMap, SettingsGroup settingsGroup, SettingsCategory settingsCategory,
        [MaybeNullWhen(false)] out IEnumerable<SettingsEntry> settingsEntries)
    {
        var settingsEntriesKey = new SettingsEntriesKey { SettingsGroup = settingsGroup, SettingsCategory = settingsCategory };

        return settingsEntriesMap.TryGetValue(settingsEntriesKey, out settingsEntries);
    }
}
