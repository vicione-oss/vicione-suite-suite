using Blazor.Shared.Module.Models;
using Sdk.Client.ControlPanels.Services;

namespace Blazor.Shared.Module.ControlPanels.Services;

public sealed class ModuleDetailsControlPanelState : ControlPanelState
{
    public bool IsDetailsExpanded { get; set; } = true;

    internal ModuleMetadataModel? ModuleMetadata { get; set; }

    public string VersionToInstall { get; set; } = string.Empty;
}
