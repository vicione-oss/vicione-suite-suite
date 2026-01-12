using Microsoft.AspNetCore.Components;

namespace Blazor.Shared.Popup.Components;

public sealed partial class PopupContentLayout : ComponentBase
{
    [Parameter, EditorRequired] public RenderFragment ChildContent { get; set; }
}
