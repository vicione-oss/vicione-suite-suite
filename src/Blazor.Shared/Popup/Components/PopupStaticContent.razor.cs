using Microsoft.AspNetCore.Components;

namespace Blazor.Shared.Popup.Components;

public sealed partial class PopupStaticContent
{
    [Parameter, EditorRequired] public RenderFragment ChildContent { get; set; }
}
