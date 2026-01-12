using Sdk.Client.ControlPanels.Services;

namespace Blazor.Shared.Module.Services;

internal sealed class ModuleManagementControlPanelCancelHandler(IModuleManagementService moduleManagementService) :
    IControlPanelResetHandler<ModuleManagementControlPanelState>
{
    public async Task Reset(ModuleManagementControlPanelState state, CancellationToken cancellationToken)
    {
        await state.CancelEditInOptionGrids();

        state.BeginLoading();
        try
        {
            if (state.IsInitialized && !state.HasChanges)
                return;

            // load metadata assets and jsons in one step
            var metadataModels = await moduleManagementService.GetModuleMetadata(state.IncludePreReleases, false, cancellationToken);

            state.Initialize(metadataModels);
        }
        finally
        {
            state.EndLoading();
        }
    }
}
