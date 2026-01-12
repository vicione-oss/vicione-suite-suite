using Microsoft.AspNetCore.Components;

namespace Blazor.Server.Backend.Components;

public sealed partial class NavigationField
{
    [Parameter, EditorRequired]
    public string Route { get; set; } = string.Empty;

    [Parameter, EditorRequired]
    public RenderFragment Content { get; set; } = default!;

    [Inject]
    private NavigationManager NavigationManager { get; set; } = default!;
}
