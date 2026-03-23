using Blazor.Shared.Module.ControlPanels.Models;
using Blazor.Shared.Module.Services;
using Sdk.Client.ControlPanels.Models;
using Sdk.Client.ControlPanels.Services;

namespace Blazor.Shared.Module.ControlPanels.Services;

internal sealed class ModuleManagementControlPanelSaveHandler(IModuleManagementService mgmtService) : IControlPanelSaveHandler<ModuleManagementControlPanelState>
{
    public async Task<ISaveResult> Save(ModuleManagementControlPanelState state, CancellationToken cancellationToken)
    {
        // pending changes
        var result = await mgmtService.UpdateOperations(state.UninstallOperations, cancellationToken);
        if (result is ModuleManagementServiceErrorResult error)
            return new SaveErrorResult(error.ErrorMessage, error.ErrorCode);

        return new SaveSuccessResult();
    }
}
