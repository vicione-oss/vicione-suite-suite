using Blazor.Shared.Settings.Extensions;
using Blazor.Shared.Settings.Models;
using Microsoft.Extensions.Logging;
using Sdk.Client.ControlPanels.Services;

namespace Blazor.Shared.Settings.Services;

internal sealed partial class SettingsModuleService(ILogger<SettingsModuleService> logger)
{
    public List<SettingsGroup> GetSettingsGroups(IEnumerable<IControlPanelRegistryItem> controlPanelRegistryItems)
    {
        var settingsGroups = new HashSet<SettingsGroup>();

        foreach (var controlPanelRegistryItem in GetNavigationItems(controlPanelRegistryItems))
        {
            try
            {
                var settingsGroup = controlPanelRegistryItem.GroupDescriptor.ToSettingsGroup();

                settingsGroups.Add(settingsGroup);
            }
            catch (Exception ex)
            {
                AddSettingsGroupFailed(logger, ex);
            }
        }

        var result = settingsGroups.OrderBy(g => g.Position).ToList();

        return result;
    }

    public Dictionary<string, SettingsCategory> GetSettingsCategoryMap(IEnumerable<IControlPanelRegistryItem> controlPanelRegistryItems,
        int groupPosition)
    {
        var categoryMap = new Dictionary<string, SettingsCategory>();

        foreach (var controlPanelRegistryItem in GetNavigationItems(controlPanelRegistryItems))
        {
            try
            {
                if (controlPanelRegistryItem.GroupDescriptor.Position == groupPosition)
                {
                    var categoryDescriptor = controlPanelRegistryItem.CategoryDescriptor;

                    if (categoryMap.ContainsKey(categoryDescriptor.Title))
                        continue;

                    var category = categoryDescriptor.ToSettingsCategory(groupPosition);

                    categoryMap.Add(categoryDescriptor.Title, category);
                }
            }
            catch (Exception ex)
            {
                CreateSettingsCategoryFailed(logger, ex);
            }
        }

        return categoryMap;
    }

    public IEnumerable<SettingsEntry> GetSettingsEntries(IEnumerable<IControlPanelRegistryItem> controlPanelRegistryItems,
        int groupPosition, string categoryTitle)
    {
        var result = new List<SettingsEntry>();

        foreach (var controlPanelRegistryItem in GetNavigationItems(controlPanelRegistryItems))
        {
            try
            {
                if (controlPanelRegistryItem.GroupDescriptor.Position == groupPosition &&
                    controlPanelRegistryItem.CategoryDescriptor.Title == categoryTitle)
                {
                    result.Add(new SettingsEntry
                    {
                        Title = controlPanelRegistryItem.Descriptor.Title,
                        IconUrl = controlPanelRegistryItem.Descriptor.IconUrl,
                        ControlPanelRegistryItem = controlPanelRegistryItem,
                        Position = controlPanelRegistryItem.Descriptor.Position
                    });
                }
            }
            catch (Exception ex)
            {
                CreateSettingsEntryFailed(logger, ex);
            }
        }

        return result;
    }

    private List<IControlPanelRegistryItem> GetNavigationItems(IEnumerable<IControlPanelRegistryItem> controlPanelRegistryItems)
    {
        var items = controlPanelRegistryItems.ToList();

        return items.Where(i => ShowInNavigationPredicate(i, items)).ToList();
    }

    private bool ShowInNavigationPredicate(IControlPanelRegistryItem controlPanelRegistryItem, List<IControlPanelRegistryItem> items)
    {
        try
        {
            // Children are not top-level navigation entries in their own right - they are only ever reached as
            // tabs under their active parent, built by SettingsContainerContentHeader from IControlPanelRegistryItem.ChildOf.
            if (controlPanelRegistryItem.ChildOf is not null)
                return false;

            // A control panel without content of its own only groups its children; without any it would open empty.
            if (!controlPanelRegistryItem.Descriptor.HasContent
                && (controlPanelRegistryItem.AsParent is null || !items.Any(i => i.ChildOf == controlPanelRegistryItem.AsParent)))
                return false;

            return controlPanelRegistryItem.Descriptor.ShowInNavigation;
        }
        catch (Exception ex)
        {
            ShowInNavigationPredicateFailed(logger, ex);

            return false;
        }
    }

    [LoggerMessage(LogLevel.Error, "Predicate to determine whether control panel should be shown in navigation or not has failed")]
    private static partial void ShowInNavigationPredicateFailed(ILogger<SettingsModuleService> logger, Exception exception);

    [LoggerMessage(LogLevel.Error, "Add settings group failed")]
    private static partial void AddSettingsGroupFailed(ILogger<SettingsModuleService> logger, Exception exception);

    [LoggerMessage(LogLevel.Error, "Create settings category failed")]
    private static partial void CreateSettingsCategoryFailed(ILogger<SettingsModuleService> logger, Exception exception);

    [LoggerMessage(LogLevel.Error, "Create settings entry failed")]
    private static partial void CreateSettingsEntryFailed(ILogger<SettingsModuleService> logger, Exception exception);
}
