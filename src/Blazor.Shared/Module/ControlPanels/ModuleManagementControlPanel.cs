using Blazor.Shared.Module.ControlPanels.Services;
using Blazor.Shared.Services;
using Sdk.Authorization;
using Sdk.Client.ControlPanels.Attributes;
using Sdk.Client.ControlPanels.Components;

namespace Blazor.Shared.Module.ControlPanels;

/// <summary>
/// The parent for child module management control panels with no content of its own.
/// </summary>
[ControlPanelCategory<ControlPanelSystemCategoryDescriptor>]
[ControlPanelParent<ModuleManagementControlPanelDescriptor>]
[ModuleAuthorize(SharedClientModule.ModuleId, AccessLevel.Full)]
public sealed class ModuleManagementControlPanel : ControlPanelBase<ModuleManagementControlPanelState>;
