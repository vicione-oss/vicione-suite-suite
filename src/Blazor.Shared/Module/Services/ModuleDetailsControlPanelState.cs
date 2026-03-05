using Blazor.Shared.Module.Models;
using Sdk.Client.ControlPanels.Services;

namespace Blazor.Shared.Module.Services;

public sealed class ModuleDetailsControlPanelState : ControlPanelState
{
    internal ModuleMetadataModel? ModuleMetadata { get; set; }
    public string VersionToInstall { get; set; } = string.Empty;
}
