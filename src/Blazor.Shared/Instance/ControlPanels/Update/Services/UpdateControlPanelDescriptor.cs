using Sdk.Client.ControlPanels.Services;
using ViciOne.Ui.Localization.Resources;

namespace Blazor.Shared.Instance.ControlPanels.Update.Services;

internal sealed class UpdateControlPanelDescriptor : IControlPanelDescriptor<UpdateControlPanel>
{
    public string Title => CommonVocabulary.General;
    public Uri? IconUrl => null;
}
