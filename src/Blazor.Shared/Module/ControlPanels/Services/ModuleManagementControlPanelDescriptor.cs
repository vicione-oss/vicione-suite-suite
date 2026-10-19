using Blazor.Shared.Enums;
using Blazor.Shared.Extensions;
using Sdk.Client.ControlPanels.Services;
using ViciOne.Ui.Localization.Resources;

namespace Blazor.Shared.Module.ControlPanels.Services;

internal sealed class ModuleManagementControlPanelDescriptor
    : IControlPanelDescriptor<ModuleManagementControlPanel>, IControlPanelParentDescriptor
{
    public string Title => TechnicalTerms.ModulePlural;
    public int? Position => 5;
    public Uri? IconUrl => SvgIcon.Modules.GetPath();
    public bool HasContent => false;
}
