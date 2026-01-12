using Microsoft.AspNetCore.Components;

namespace Blazor.Shared.Wizards.ContentOverlays.Components;

public sealed partial class OptionalSectionContent
{
    [Parameter] public object? SectionId { get; set; }
    [Parameter, EditorRequired] public RenderFragment ChildContent { get; set; }
}
