using Sdk.Client.ControlPanels.Services;
using ViciOne.Ui.Localization.Resources;

namespace Blazor.Shared.Instance.ControlPanels.Update.Services;

internal sealed class UpdateResetControlPanelDescriptor : IControlPanelDescriptor<UpdateResetControlPanel>
{
    public string Title => CommonVocabulary.ResetVerb;
    public Uri? IconUrl => null;
    public int? Position => 2;
    public bool ShowInNavigation => false;
}
