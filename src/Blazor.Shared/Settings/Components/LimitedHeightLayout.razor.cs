using Microsoft.AspNetCore.Components;

namespace Blazor.Shared.Settings.Components;

// TODO(#2802): Move to the Settings namespace of suite-sdk once the approach is accepted.
/// <summary>
/// Limits its content to the available height, so the content has to scroll vertically and keep within the width by itself.
/// </summary>
/// <remarks>
/// Children shrink to fit; wrap content that does not scroll by itself in <see cref="NonShrinkingContent"/> to keep its height.
/// </remarks>
public sealed partial class LimitedHeightLayout
{
    [Parameter, EditorRequired] public RenderFragment ChildContent { get; set; } = default!;
}
