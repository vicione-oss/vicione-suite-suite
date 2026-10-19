using Blazor.Shared.Module.ControlPanels.Extensions;
using Blazor.Shared.Module.Services;
using Sdk.Client.ControlPanels.Models;
using Sdk.Client.ControlPanels.Services;

namespace Blazor.Shared.Module.ControlPanels.Services;

internal sealed class ModuleManagementBrowseControlPanelSaveHandler(IModuleManagementService managementService)
    : IControlPanelSaveHandler<ModuleManagementBrowseControlPanelState>
{
    public Task<ISaveResult> Save(ModuleManagementBrowseControlPanelState state, CancellationToken cancellationToken)
        => state.SaveEnqueuedOperations(managementService, cancellationToken);
}
