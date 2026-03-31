using Microsoft.AspNetCore.Components;

namespace Blazor.Shared.Dialogs;

public sealed partial class OkDialog
{
    [Parameter] public string CssClass { get; set; } = "ok-dialog";

    [Parameter] public RenderFragment? Body { get; set; }

    [Parameter] public string? HeaderText { get; set; }

    [Parameter] public EventCallback OnConfirm { get; set; }

    [Parameter] public bool Visible { get; set; }
    [Parameter] public EventCallback<bool> VisibleChanged { get; set; }

    private async Task OkButtonClick()
    {
        if (OnConfirm.HasDelegate)
            await OnConfirm.InvokeAsync();

        await UpdateVisible(false);
    }

    private async Task UpdateVisible(bool value)
    {
        Visible = value;

        if (VisibleChanged.HasDelegate)
            await VisibleChanged.InvokeAsync(Visible);
    }
}
