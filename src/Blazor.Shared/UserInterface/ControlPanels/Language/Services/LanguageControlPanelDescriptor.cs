using Blazor.Shared.UserInterface.ControlPanels.Language.Components;
using Sdk.Client.ControlPanels.Services;
using ViciOne.Ui.Localization.Resources;

namespace Blazor.Shared.UserInterface.ControlPanels.Language.Services;

internal sealed class LanguageControlPanelDescriptor : IControlPanelDescriptor<LanguageControlPanel>
{
    public string Title => CommonVocabulary.Language;
    public string IconPath => string.Empty;
    public int? Position => null;
}
