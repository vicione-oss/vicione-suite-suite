using Blazor.Shared.Settings.Models;

namespace Blazor.Shared.Settings.Extensions;

internal static class SettingsEntriesKeyExtensions
{
    public static SettingsCategoryKey ToSettingsCategoryKey(this SettingsEntriesKey settingsEntriesKey)
        => new() { GroupPosition = settingsEntriesKey.SettingsGroup.Position, Title = settingsEntriesKey.SettingsCategory.Title };
}
