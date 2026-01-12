using Microsoft.AspNetCore.Components;
using ViciOne.Ui.MonochromeIcons.Core.Enums;

namespace Blazor.Shared.Popup.Components;

public sealed partial class PopupActionButton : ComponentBase
{
    [Parameter, EditorRequired]
    public MonochromeIconName IconName { get; set; }
    [Parameter]
    public EventCallback OnClick { get; set; }

    private async Task ButtonClick()
    {
        if (OnClick.HasDelegate)
            await OnClick.InvokeAsync();
    }
}
