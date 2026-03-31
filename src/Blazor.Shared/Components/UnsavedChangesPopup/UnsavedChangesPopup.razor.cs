using Microsoft.AspNetCore.Components;

namespace Blazor.Shared.Components.UnsavedChangesPopup;

public sealed partial class UnsavedChangesPopup
{
    private bool _visible;
    private bool _invokeOnCancelWhenClosing = true;

    [Parameter]
    public EventCallback OnCancel { get; set; }

    [Parameter]
    public EventCallback OnRevert { get; set; }

    [Parameter]
    public EventCallback OnSave { get; set; }

    [Parameter]
    public bool Visible { get; set; }

    protected override void OnParametersSet()
    {
        if (Visible != _visible)
        {
            _visible = Visible;

            _invokeOnCancelWhenClosing = _visible;
        }
    }

    private async Task SaveButtonClick()
    {
        _invokeOnCancelWhenClosing = false;

        if (OnSave.HasDelegate)
            await OnSave.InvokeAsync();
    }

    private async Task RevertButtonClick()
    {
        _invokeOnCancelWhenClosing = false;

        if (OnRevert.HasDelegate)
            await OnRevert.InvokeAsync();
    }

    private async Task CancelButtonClick()
    {
        _invokeOnCancelWhenClosing = false;

        if (OnCancel.HasDelegate)
            await OnCancel.InvokeAsync();
    }

    private async Task DialogClosing()
    {
        if (_invokeOnCancelWhenClosing && OnCancel.HasDelegate)
            await OnCancel.InvokeAsync();
    }
}
