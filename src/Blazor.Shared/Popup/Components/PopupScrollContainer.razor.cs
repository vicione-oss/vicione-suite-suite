using Microsoft.AspNetCore.Components;

namespace Blazor.Shared.Popup.Components;

public sealed partial class PopupScrollContainer
{
    [Parameter, EditorRequired] public RenderFragment Content { get; set; }
    [Parameter] public RenderFragment? AfterContent { get; set; }
    [Parameter] public string? CssClass { get; set; }
}
