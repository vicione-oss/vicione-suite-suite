using Microsoft.Extensions.Logging;
using Sdk.Client.ControlPanels.Models;
using Sdk.Client.ControlPanels.Services;
using Sdk.Client.Services;

namespace Blazor.Shared.Module.Services;

internal class ModuleManagementControlPanelSaveHandler(IModuleManagementService moduleManagementService,
    IMessageBannerService messageBannerService, ILogger<ModuleManagementControlPanelSaveHandler> logger)
        : IControlPanelSaveHandler<ModuleManagementControlPanelState>
{
    public async Task<ISaveResult> Save(ModuleManagementControlPanelState state, CancellationToken cancellationToken)
    {
        await state.CancelEditInOptionGrids();

        try
        {
            await moduleManagementService.UpdateModulePackageVersions([.. state.InstalledModules, .. state.AvailableModules], cancellationToken);

            messageBannerService.ShowMessageBanner(Sdk.MessageBanner.Contracts.MessageType.Warning, ControlPanels.Localization.ModuleManagementControlPanel.RestartSystemToApplyModuleChanges);

            return new SaveSuccessResult();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to set module versions");

            return new SaveErrorResult(Localization.ModuleManagementControlPanelSaveHandler.FailedToSetModuleVersions);
        }
    }
}
