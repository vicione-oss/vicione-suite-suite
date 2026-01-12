using Microsoft.Extensions.Logging;
using Sdk.Client.ControlPanels.Models;
using Sdk.Client.ControlPanels.Services;

namespace Blazor.Shared.Module.Services;

internal class ModuleManagementControlPanelSaveHandler(IModuleManagementService moduleManagementService, ILogger<ModuleManagementControlPanelSaveHandler> logger)
        : IControlPanelSaveHandler<ModuleManagementControlPanelState>
{
    public async Task<ISaveResult> Save(ModuleManagementControlPanelState state, CancellationToken cancellationToken)
    {
        await state.CancelEditInOptionGrids();

        try
        {
            await moduleManagementService.UpdateModulePackageVersions([.. state.InstalledModules, .. state.AvailableModules], cancellationToken);

            return new SaveSuccessResult();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to set module versions");

            return new SaveErrorResult(Localization.ModuleManagementControlPanelSaveHandler.FailedToSetModuleVersions);
        }
    }
}
