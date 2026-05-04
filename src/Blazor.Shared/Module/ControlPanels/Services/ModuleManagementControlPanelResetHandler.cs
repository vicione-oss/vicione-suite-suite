using Blazor.Shared.Module.ControlPanels.Extensions;
using Blazor.Shared.Module.Services;
using Sdk.Client.ControlPanels.Services;

namespace Blazor.Shared.Module.ControlPanels.Services;

internal sealed class ModuleManagementControlPanelResetHandler(IModuleManagementService moduleManagementService) :
    IControlPanelResetHandler<ModuleManagementControlPanelState>
{
    public async Task Reset(ModuleManagementControlPanelState state, CancellationToken cancellationToken)
    {
        state.BeginLoading();
        try
        {
            await state.CancelEditInOptionGrids();

            // load metadata assets and jsons in one step
            var metadataModels = await moduleManagementService.GetMetadata(false, cancellationToken);
            state.UpdateModules(metadataModels);
        }
        finally
        {
            state.EndLoading();
        }
    }
}
