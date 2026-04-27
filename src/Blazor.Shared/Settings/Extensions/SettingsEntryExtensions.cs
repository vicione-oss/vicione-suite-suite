using Blazor.Shared.Settings.Comparers;
using Blazor.Shared.Settings.Models;

namespace Blazor.Shared.Settings.Extensions;

internal static class SettingsEntryExtensions
{
    public static IOrderedEnumerable<SettingsEntry> Sort(this IEnumerable<SettingsEntry> settingsEntries)
        => settingsEntries.OrderBy(settingsEntry => settingsEntry.Position, new OptionalPositionComparer())
            .ThenBy(settingsEntry => settingsEntry.Title);
}
