using Blazor.Shared.Enums;
using Blazor.Shared.Extensions;
using Sdk.Client.ControlPanels.Services;
using ViciOne.Ui.Localization.Resources;

namespace Blazor.Shared.Module.ControlPanels.Services;

internal sealed class ModuleManagementInstalledControlPanelDescriptor : IControlPanelDescriptor<ModuleManagementInstalledControlPanel>
{
    public string Title => CommonVocabulary.Installed;
    public Uri? IconUrl => SvgIcon.Modules.GetPath();
    public int? Position => 0;
    public bool ShowInNavigation => false;
}
