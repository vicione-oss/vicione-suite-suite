using Blazor.Shared.Enums;
using Blazor.Shared.Extensions;
using Sdk.Client.ControlPanels.Services;
using ViciOne.Ui.Localization.Resources;

namespace Blazor.Shared.Module.ControlPanels.Services;

internal sealed class ModuleManagementControlPanelDescriptor : IControlPanelDescriptor<ModuleManagementControlPanel>
{
    public string Title => TechnicalTerms.ModulePlural;
    public Uri? IconUrl => SvgIcon.Modules.GetPath();
}
