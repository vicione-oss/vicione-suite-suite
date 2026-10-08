using Blazor.Shared.Connections.ControlPanels.Tags.Services;
using Blazor.Shared.Services;
using Sdk.Client.ControlPanels.Attributes;
using Sdk.Client.ControlPanels.Components;

namespace Blazor.Shared.Connections.ControlPanels.Tags;

[ControlPanelCategory<ControlPanelSystemCategoryDescriptor>]
public sealed partial class TagControlPanel : ControlPanelBase<TagControlPanelState>
{
    private string GetDescriptionBannerTitle(string? text)
    {
        var culture = Localization.TagControlPanel.Culture;
        return string.Format(culture, State.IsEditMode
            ? ViciOne.Ui.Localization.Resources.UserActions.EditSomething
            : Localization.TagControlPanel.DescriptionBannerTitleOnAdd, text);
    }
}
