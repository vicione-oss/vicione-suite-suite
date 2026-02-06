using Blazor.Shared.Enums;
using Blazor.Shared.Extensions;
using Blazor.Shared.Module.ControlPanels;
using Sdk.Client.ControlPanels.Services;
using ViciOne.Ui.Localization.Resources;

namespace Blazor.Shared.Module.Services;

internal sealed class ModuleManagementControlPanelDescriptor : IControlPanelDescriptor<ModuleManagementControlPanel>
{
    public string Title => TechnicalTerms.ModulePlural;
    public Uri IconUrl => SvgIcon.Modules.GetPath();
}
