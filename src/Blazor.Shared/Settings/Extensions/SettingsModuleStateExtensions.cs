using Blazor.Shared.Settings.Models;
using Blazor.Shared.Settings.Services;
using Microsoft.Extensions.Logging;
using Sdk.Client.ControlPanels.Services;

namespace Blazor.Shared.Settings.Extensions;

internal static class SettingsModuleStateExtensions
{
    extension(SettingsModuleState settingsModuleState)
    {
        public void SetActiveControlPanel(IControlPanelRegistryItem? controlPanelRegistryItem)
        {
            settingsModuleState.BeginUpdate();
            try
            {
                ResetActiveControlPanelPage(settingsModuleState.ActiveControlPanelRegistryItem);

                settingsModuleState.ActiveControlPanelRegistryItem = controlPanelRegistryItem;

                settingsModuleState.ClearRequestedControlPanelRegistryItems();
                if (controlPanelRegistryItem is not null)
                    settingsModuleState.TryPushRequestedControlPanelRegistryItem(controlPanelRegistryItem);

                settingsModuleState.ShowNavigateBackButton = false;

                ResetActiveControlPanelPage(settingsModuleState.ActiveControlPanelRegistryItem);
            }
            finally
            {
                settingsModuleState.EndUpdate();
            }
        }

        private static void ResetActiveControlPanelPage(IControlPanelRegistryItem? controlPanelRegistryItem)
            => controlPanelRegistryItem?.State.ActivePageIndex = null;

        /// <remarks>
        /// This method only supports requests for control panels configured to be displayed in settings categories.
        ///
        /// Deeper navigation levels will be supported with https://gitlab.com/vicione-oss/vicione/suite/suite-sdk/-/work_items/324.
        /// </remarks>
        public void AdoptToControlPanelRequest(ControlPanelRequestedEventArgs args, ILogger logger)
        {
            var controlPanelRegistryItem = args.RegistryItem;

            settingsModuleState.BeginUpdate();
            try
            {
                var settingsGroup = controlPanelRegistryItem.GroupDescriptor.ToSettingsGroup();
                var settingsCategory = controlPanelRegistryItem.CategoryDescriptor.ToSettingsCategory(settingsGroup.Position);
                var settingsEntryKey = new SettingsEntriesKey { SettingsGroup = settingsGroup, SettingsCategory = settingsCategory };

                settingsModuleState.PreselectFirstSettingsEntryInFirstSettingsGroup = false;
                settingsModuleState.ExpandedSettingsCategory = settingsCategory;
                settingsModuleState.SetActiveControlPanel(controlPanelRegistryItem);

                settingsModuleState.AddOrSetLastActiveControlPanelRegistryItem(settingsEntryKey, controlPanelRegistryItem);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An error occurred why trying to adopt to control panel request.");
            }
            finally
            {
                settingsModuleState.EndUpdate();
            }
        }
    }
}
