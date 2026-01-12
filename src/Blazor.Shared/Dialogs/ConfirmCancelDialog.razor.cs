using Blazor.Shared.Extensions;
using Microsoft.AspNetCore.Components;
using ViciOne.Ui.Shared.Dx.Components;

namespace Blazor.Shared.Dialogs;

public sealed partial class ConfirmCancelDialog : ComponentBase
{
    private DxDialog? _refDialog;

    [Parameter] public RenderFragment? Body { get; set; }

    [Parameter] public string? HeaderText { get; set; }

    [Parameter] public EventCallback OnCancel { get; set; }

    [Parameter] public EventCallback OnConfirm { get; set; }

    [Parameter] public bool Show { get; set; }

    protected override async Task OnAfterRenderAsync(bool firstRender)
        => await _refDialog.OpenOrCloseDialog(Show);
}
