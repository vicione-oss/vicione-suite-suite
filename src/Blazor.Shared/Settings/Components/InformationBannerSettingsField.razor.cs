using Microsoft.AspNetCore.Components;

namespace Blazor.Shared.Settings.Components;

public sealed partial class InformationBannerSettingsField : ComponentBase
{
    [Parameter]
    public bool Visible { get; set; } = true;
    [Parameter, EditorRequired]
    public string Text { get; set; }
    [Parameter]
    public RenderFragment? Button { get; set; }
}
