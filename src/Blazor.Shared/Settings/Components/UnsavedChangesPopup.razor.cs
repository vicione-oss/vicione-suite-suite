using Blazor.Shared.Extensions;
using Microsoft.AspNetCore.Components;
using ViciOne.Ui.Shared.Dx.Components;

namespace Blazor.Shared.Settings.Components;

public sealed partial class UnsavedChangesPopup
{
    private DxDialog? _refDialog;

    [Parameter]
    public EventCallback OnCancel { get; set; }

    [Parameter]
    public EventCallback OnRevert { get; set; }

    [Parameter]
    public EventCallback OnSave { get; set; }

    [Parameter]
    public bool Visible { get; set; }

    protected override async Task OnAfterRenderAsync(bool firstRender)
        => await _refDialog.OpenOrCloseDialog(Visible);
}
