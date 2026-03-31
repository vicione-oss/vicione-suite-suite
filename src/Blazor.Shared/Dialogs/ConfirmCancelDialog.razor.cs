using Microsoft.AspNetCore.Components;

namespace Blazor.Shared.Dialogs;

public sealed partial class ConfirmCancelDialog : ComponentBase
{
    private bool _visible;
    private bool _invokeOnCancelWhenClosing = true;

    [Parameter] public RenderFragment? Body { get; set; }

    [Parameter] public string? HeaderText { get; set; }

    [Parameter] public EventCallback OnCancel { get; set; }

    [Parameter] public EventCallback OnConfirm { get; set; }

    [Parameter] public bool Visible { get; set; }
    [Parameter] public EventCallback<bool> VisibleChanged { get; set; }

    protected override void OnParametersSet()
    {
        if (Visible != _visible)
        {
            _visible = Visible;

            _invokeOnCancelWhenClosing = _visible;
        }
    }

    private async Task ConfirmButtonClick()
    {
        _invokeOnCancelWhenClosing = false;

        if (OnConfirm.HasDelegate)
            await OnConfirm.InvokeAsync();

        await UpdateVisible(false);
    }

    private async Task CancelButtonClick()
    {
        _invokeOnCancelWhenClosing = false;

        if (OnCancel.HasDelegate)
            await OnCancel.InvokeAsync();

        await UpdateVisible(false);
    }

    private async Task DialogClosing()
    {
        if (_invokeOnCancelWhenClosing && OnCancel.HasDelegate)
            await OnCancel.InvokeAsync();
    }

    private async Task UpdateVisible(bool visible)
    {
        _visible = visible;

        if (VisibleChanged.HasDelegate)
            await VisibleChanged.InvokeAsync(_visible);
    }
}
