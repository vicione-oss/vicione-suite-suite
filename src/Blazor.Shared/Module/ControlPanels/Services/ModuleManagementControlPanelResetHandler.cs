using Blazor.Shared.Module.Services;
using Sdk.Client.ControlPanels.Services;

namespace Blazor.Shared.Module.ControlPanels.Services;

internal sealed class ModuleManagementControlPanelResetHandler(IModuleManagementService moduleManagementService) :
    IControlPanelResetHandler<ModuleManagementControlPanelState>
{
    public async Task Reset(ModuleManagementControlPanelState state, CancellationToken cancellationToken)
    {
        await state.CancelEditInOptionGrids();

        state.BeginLoading();
        try
        {
            if (state.IsInitialized)
                return;

            // load metadata assets and jsons in one step
            var metadataModels = await moduleManagementService.GetModuleMetadata(false, cancellationToken);

            state.Initialize(metadataModels);
        }
        finally
        {
            state.EndLoading();
        }
    }
}
