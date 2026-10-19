using Blazor.Shared.Module.ControlPanels.Extensions;
using Blazor.Shared.Module.Services;
using Microsoft.Extensions.Logging;
using Sdk.Client.ControlPanels.Services;

namespace Blazor.Shared.Module.ControlPanels.Services;

internal sealed class ModuleManagementBrowseControlPanelResetHandler(IModuleManagementService managementService,
    ILogger<ModuleManagementBrowseControlPanelResetHandler> logger)
    : IControlPanelResetHandler<ModuleManagementBrowseControlPanelState>
{
    public Task Reset(ModuleManagementBrowseControlPanelState state, CancellationToken cancellationToken)
        => state.LoadAvailableModules(managementService, false, logger, cancellationToken);
}
