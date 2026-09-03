using Microsoft.AspNetCore.Components;

namespace Blazor.Shared.NavTiles.Components;

public sealed partial class NavTilePanelGrid
{
    [Parameter, EditorRequired] public RenderFragment ChildContent { get; set; }
}
