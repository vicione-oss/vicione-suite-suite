using Blazor.Shared.Instance.ControlPanels;
using Sdk.Client.ControlPanels.Services;
using ViciOne.Ui.Localization.Resources;

namespace Blazor.Shared.Instance.Services;

internal sealed class UpdateControlPanelDescriptor : IControlPanelDescriptor<UpdateControlPanel>
{
    public string Title => CommonVocabulary.General;
    public string IconPath => string.Empty;
}
