using Blazor.Shared.Module.ControlPanels.Extensions;
using Blazor.Shared.Module.Services;
using Sdk.Client.ControlPanels.Services;

namespace Blazor.Shared.Module.ControlPanels.Services;

internal sealed class ModuleManagementInstalledControlPanelResetHandler(IModuleManagementService managementService)
    : IControlPanelResetHandler<ModuleManagementInstalledControlPanelState>
{
    public async Task Reset(ModuleManagementInstalledControlPanelState state, CancellationToken cancellationToken)
    {
        state.BeginLoading();
        try
        {
            await state.CancelEditInOptionGrids();

            var result = await managementService.GetInstalledMetadata(cancellationToken);
            state.SetModules(result.Modules);
        }
        finally
        {
            state.EndLoading();
        }
    }
}
