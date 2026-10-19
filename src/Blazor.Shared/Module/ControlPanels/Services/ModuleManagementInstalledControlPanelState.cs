using Blazor.Shared.Module.Services;

namespace Blazor.Shared.Module.ControlPanels.Services;

public sealed class ModuleManagementInstalledControlPanelState(IModuleManagementService managementService)
    : ModuleManagementControlPanelStateBase(managementService);
