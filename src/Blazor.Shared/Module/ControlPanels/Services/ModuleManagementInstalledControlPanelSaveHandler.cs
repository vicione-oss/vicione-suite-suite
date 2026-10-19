using Blazor.Shared.Module.ControlPanels.Extensions;
using Blazor.Shared.Module.Services;
using Sdk.Client.ControlPanels.Models;
using Sdk.Client.ControlPanels.Services;

namespace Blazor.Shared.Module.ControlPanels.Services;

internal sealed class ModuleManagementInstalledControlPanelSaveHandler(IModuleManagementService managementService)
    : IControlPanelSaveHandler<ModuleManagementInstalledControlPanelState>
{
    public Task<ISaveResult> Save(ModuleManagementInstalledControlPanelState state, CancellationToken cancellationToken)
        => state.SaveEnqueuedOperations(managementService, cancellationToken);
}
