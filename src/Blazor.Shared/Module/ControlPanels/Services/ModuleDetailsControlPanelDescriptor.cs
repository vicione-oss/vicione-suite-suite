using Blazor.Shared.Enums;
using Blazor.Shared.Extensions;
using Sdk.Client.ControlPanels.Services;

namespace Blazor.Shared.Module.ControlPanels.Services;

public sealed class ModuleDetailsControlPanelDescriptor : IControlPanelDescriptor<ModuleDetailsControlPanel>
{
    public string Title => "Details";
    public Uri IconUrl => SvgIcon.Modules.GetPath();
    public bool ShowInNavigation => false;
}

