using Microsoft.AspNetCore.Components;

namespace Blazor.Shared.Popup.Components;

public sealed partial class PopupScrollableContent
{
    [Parameter, EditorRequired] public RenderFragment ChildContent { get; set; }
}
