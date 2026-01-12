using Microsoft.AspNetCore.Components;
using Sdk.Client.Wizards.Components;
using PopupComponent = ViciOne.Ui.Blazor.Components.Popup.Components.Popup;

namespace Blazor.Shared.Wizards.Components;

public sealed partial class WizardContent<TContext> : ComponentBase, IWizardContent<TContext>
{
    private PopupComponent? _popup;
    private WizardBody<TContext>? _wizardBody;
    private bool _showUnsavedChangesPopup;

    [Parameter, EditorRequired] public string Title { get; set; }
    [Parameter, EditorRequired] public TContext Context { get; set; }
    [Parameter] public bool Visible { get; set; }
    [Parameter] public EventCallback<bool> VisibleChanged { get; set; }
    [Parameter] public bool AllowExit { get; set; }

    private async Task PopupVisibleChanged()
    {
        if (VisibleChanged.HasDelegate)
            await VisibleChanged.InvokeAsync(Visible);
    }

    private async Task WizardBodyOnExit()
    {
        if (_popup is not null)
            await _popup.HideAsync();
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
    }

    private async Task OnSaveUnsavedChanges()
    {
        _showUnsavedChangesPopup = false;

        if (_wizardBody is not null)
            await _wizardBody.OnSaveUnsavedChangesPopup();
    }
}
