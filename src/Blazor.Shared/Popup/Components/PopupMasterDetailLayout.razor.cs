using Microsoft.AspNetCore.Components;

namespace Blazor.Shared.Popup.Components;

public sealed partial class PopupMasterDetailLayout : ComponentBase
{
    [Parameter]
    public string? PopupTitle { get; set; }
    [Parameter]
    public RenderFragment? BeforeNavigation { get; set; }
    [Parameter, EditorRequired]
    public RenderFragment Navigation { get; set; }
    [Parameter]
    public RenderFragment? PopupActionButtons { get; set; }
    [Parameter, EditorRequired]
    public RenderFragment ContentHeader { get; set; }
    [Parameter]
    public RenderFragment? BeforeContent { get; set; }
    [Parameter, EditorRequired]
    public RenderFragment Content { get; set; }
    [Parameter]
    public bool ContentActionButtonsVisible { get; set; } = true;
    [Parameter]
    public RenderFragment? ContentActionButtons { get; set; }
    [Parameter]
    public RenderFragment? LoadingOverlay { get; set; }
}
