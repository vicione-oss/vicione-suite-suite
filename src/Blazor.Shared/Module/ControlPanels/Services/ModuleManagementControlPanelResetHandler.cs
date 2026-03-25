using Blazor.Shared.Module.Services;
using Sdk.Client.ControlPanels.Services;

namespace Blazor.Shared.Module.ControlPanels.Services;

internal sealed class ModuleManagementControlPanelResetHandler(IModuleManagementService moduleManagementService) :
    IControlPanelResetHandler<ModuleManagementControlPanelState>
{
    public async Task Reset(ModuleManagementControlPanelState state, CancellationToken cancellationToken)
    {
        // attempt to avoid unnecessary reload if there are no pending changes and we already have metadata loaded
        if (!state.HasPendingChanges() && (state.InstalledModules.Count > 0 || state.AvailableModules.Count > 0))
            return;

        state.BeginLoading();
        try
        {
            await state.CancelEditInOptionGrids();

            // load metadata assets and jsons in one step
            var metadataModels = await moduleManagementService.GetMetadata(false, cancellationToken);
            state.Initialize(metadataModels);
        }
        finally
        {
            state.EndLoading();
        }
    }
}
