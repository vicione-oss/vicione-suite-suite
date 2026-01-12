using Microsoft.AspNetCore.Components;

namespace Blazor.Shared.Components;

public sealed partial class ExternalLinkButton
{
    [Parameter] public string? CssClass { get; set; }
    [Parameter, EditorRequired] public string Text { get; set; }
    [Parameter] public EventCallback OnClick { get; set; }

    private async Task ButtonClick()
    {
        if (OnClick.HasDelegate)
            await OnClick.InvokeAsync();
    }
}
