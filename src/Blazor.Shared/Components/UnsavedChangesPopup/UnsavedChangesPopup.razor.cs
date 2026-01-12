using Blazor.Shared.Extensions;
using Microsoft.AspNetCore.Components;
using ViciOne.Ui.Shared.Dx.Components;

namespace Blazor.Shared.Components.UnsavedChangesPopup;

public sealed partial class UnsavedChangesPopup
{
    private DxDialog? _refDialog;
    private bool _visible;
    private bool _invokeOnCancelOnClosing = true;

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

            _invokeOnCancelOnClosing = _visible;
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
        => await _refDialog.OpenOrCloseDialog(Visible);

    private async Task SaveButtonClick()
    {
        _invokeOnCancelOnClosing = false;

        if (OnSave.HasDelegate)
            await OnSave.InvokeAsync();
    }

    private async Task RevertButtonClick()
    {
        _invokeOnCancelOnClosing = false;

        if (OnRevert.HasDelegate)
            await OnRevert.InvokeAsync();
    }

    private async Task CancelButtonClick()
    {
        _invokeOnCancelOnClosing = false;

        if (OnCancel.HasDelegate)
            await OnCancel.InvokeAsync();
    }

    private async Task Closing()
    {
        if (_invokeOnCancelOnClosing && OnCancel.HasDelegate)
            await OnCancel.InvokeAsync();
    }
}
