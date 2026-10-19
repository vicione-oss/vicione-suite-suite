using Blazor.Shared.Enums;
using Blazor.Shared.Extensions;
using Sdk.Client.ControlPanels.Services;
using ViciOne.Ui.Localization.Resources;

namespace Blazor.Shared.Module.ControlPanels.Services;

internal sealed class ModuleManagementBrowseControlPanelDescriptor : IControlPanelDescriptor<ModuleManagementBrowseControlPanel>
{
    public string Title => CommonVocabulary.BrowseVerb;
    public Uri? IconUrl => SvgIcon.Modules.GetPath();
    public int? Position => 1;
    public bool ShowInNavigation => false;
}
