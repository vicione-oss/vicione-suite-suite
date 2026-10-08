using Microsoft.AspNetCore.Components;

namespace Blazor.Shared.Settings.Components;

// TODO(#2802): Move to the Settings namespace of suite-sdk once the approach is accepted.
/// <summary>
/// Keeps its content at full height inside a <see cref="LimitedHeightLayout"/>, so only the other children give up room.
/// </summary>
public sealed partial class NonShrinkingContent
{
    [Parameter, EditorRequired] public RenderFragment ChildContent { get; set; } = default!;
}
