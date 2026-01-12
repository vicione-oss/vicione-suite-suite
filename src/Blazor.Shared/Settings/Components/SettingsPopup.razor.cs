using Blazor.Shared.Settings.Services;
using Microsoft.AspNetCore.Components;
using Sdk.Client.Models;

namespace Blazor.Shared.Settings.Components;

public sealed partial class SettingsPopup : ComponentBase, IDisposable
{
    private SettingsContainer? _settingsContainer;
    private bool _showUnsavedChangesPopup;

    [Inject] private ISettingsPopupRequest SettingsPopupRequest { get; set; } = default!;
    [Inject] private ISettingsPopupState SettingsPopupState { get; set; } = default!;

    protected override void OnInitialized()
    {
        SettingsPopupRequest.SettingsPopupRequested += SettingsPopupRequested;
        SettingsPopupState.Changed += SettingsPopupStateChanged;
    }

    public void Dispose()
    {
        SettingsPopupState.Changed -= SettingsPopupStateChanged;
        SettingsPopupRequest.SettingsPopupRequested -= SettingsPopupRequested;
    }

    private void SettingsPopupRequested()
        => SettingsPopupState.Visible = true;

    private void SettingsPopupStateChanged(PropertiesChangedEventArgs args)
    {
        if (args.PropertyNames.Contains(nameof(SettingsPopupState.Visible)))
            InvokeAsync(StateHasChanged);
    }

    private void OnCancelUnsavedChanges()
    {
        _showUnsavedChangesPopup = false;
        _settingsContainer?.OnCancelUnsavedChangesPopup();
    }

    private void SettingsContainerOnClose()
        => SettingsPopupState.Visible = false;

    private async Task OnRevertUnsavedChanges()
    {
        _showUnsavedChangesPopup = false;

        if (_settingsContainer is not null)
            await _settingsContainer.OnRevertUnsavedChangesPopup();
    }

    private async Task OnSaveUnsavedChanges()
    {
        _showUnsavedChangesPopup = false;

        if (_settingsContainer is not null)
            await _settingsContainer.OnSaveUnsavedChangesPopup();
    }
}
