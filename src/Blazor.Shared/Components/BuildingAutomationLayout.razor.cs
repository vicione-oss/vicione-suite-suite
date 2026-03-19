using Microsoft.AspNetCore.Components;

namespace Blazor.Shared.Components;

public partial class BuildingAutomationLayout
{
    [Parameter, EditorRequired]
    public required string Heading { get; set; }

    [Parameter, EditorRequired]
    public required RenderFragment ChildContent { get; set; }
}
