using Blazor.Shared.Settings.Comparers;
using Blazor.Shared.Settings.Models;

namespace Blazor.Shared.Settings.Extensions;

internal static class SettingsCategoryExtensions
{
    public static IOrderedEnumerable<SettingsCategory> Sort(this IEnumerable<SettingsCategory> settingsCategories)
        => settingsCategories.OrderBy(settingsCategory => settingsCategory.Position, new OptionalPositionComparer())
            .ThenBy(settingsCategory => settingsCategory.Title);
}
