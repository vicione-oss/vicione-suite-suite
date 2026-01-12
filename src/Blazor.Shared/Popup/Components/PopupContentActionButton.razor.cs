using Microsoft.AspNetCore.Components;
using ViciOne.Ui.MonochromeIcons.Core.Enums;

namespace Blazor.Shared.Popup.Components;

public sealed partial class PopupContentActionButton : ComponentBase
{
    [CascadingParameter(Name = PopupContentActionButtonContainer.CascadingEnabledParameterName)]
    private bool CascadingEnabled { get; set; }

    [Parameter]
    public string? CssClass { get; set; }
    [Parameter, EditorRequired]
    public MonochromeIconName IconName { get; set; }
    [Parameter, EditorRequired]
    public string Text { get; set; }
    [Parameter]
    public bool? Enabled { get; set; }
    [Parameter]
    public EventCallback OnClick { get; set; }

    private async Task ButtonClick()
    {
        if (OnClick.HasDelegate)
            await OnClick.InvokeAsync();
    }
}
