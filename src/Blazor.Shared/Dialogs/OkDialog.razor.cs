using Blazor.Shared.Extensions;
using Microsoft.AspNetCore.Components;
using ViciOne.Ui.Shared.Dx.Components;

namespace Blazor.Shared.Dialogs;

public sealed partial class OkDialog
{
    private DxDialog? _refDialog;

    [Parameter] public RenderFragment? Body { get; set; }

    [Parameter] public string? HeaderText { get; set; }

    [Parameter] public EventCallback OnConfirm { get; set; }

    [Parameter] public bool Show { get; set; }

    [Parameter] public int ZIndex { get; set; }

    protected override async Task OnAfterRenderAsync(bool firstRender)
        => await _refDialog.OpenOrCloseDialog(Show);


    public void Refresh()
        => InvokeAsync(StateHasChanged);
}
