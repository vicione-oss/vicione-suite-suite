using DevExpress.Blazor;
using Microsoft.AspNetCore.Components;

namespace Blazor.Shared.Wizard.Components;

public sealed partial class Wizard<TContext> : IWizard
{
    private DxPopup? _dxPopup;
    private WizardBody<TContext>? _wizardBody;
    private bool _showUnsavedChangesPopup;

    [Parameter, EditorRequired] public string Title { get; set; }
    [Parameter, EditorRequired] public TContext Context { get; set; }
    [Parameter] public bool Visible { get; set; }
    [Parameter] public EventCallback<bool> VisibleChanged { get; set; }
    [Parameter] public bool AllowExit { get; set; }

    private void OnClosing(PopupClosingEventArgs args)
    {
        if (ShouldCancelClose())
            args.Cancel = true;
    }

    private async Task DxPopupVisibleChanged()
    {
        if (VisibleChanged.HasDelegate)
            await VisibleChanged.InvokeAsync(Visible);
    }

    private async Task WizardBodyOnExit()
    {
        if (_dxPopup is not null)
            await _dxPopup.CloseAsync();
    }

    private static bool ShouldCancelClose()
    {
        var isDirty = false;

        return isDirty;
    }

    private void OnCancelUnsavedChanges()
    {
        _showUnsavedChangesPopup = false;
        _wizardBody?.OnCancelUnsavedChangesPopup();
    }

    private async Task OnRevertUnsavedChanges()
    {
        _showUnsavedChangesPopup = false;

        if (_wizardBody is not null)
            await _wizardBody.OnRevertUnsavedChangesPopup();

        await InvokeAsync(StateHasChanged);
    }

    private async Task OnSaveUnsavedChanges()
    {
        if (_wizardBody is not null)
            await _wizardBody.OnSaveUnsavedChangesPopup();

        _showUnsavedChangesPopup = false;

        await InvokeAsync(StateHasChanged);
    }
}
